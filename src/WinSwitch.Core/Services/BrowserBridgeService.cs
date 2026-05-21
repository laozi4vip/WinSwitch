using System.Collections.Concurrent;
using System.IO.Pipes;
using Newtonsoft.Json;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 浏览器桥接服务 - 多客户端版本
///
/// 支持：
/// 1. 多个浏览器同时连接，例如 Chrome / Edge / Brave
/// 2. 同浏览器多个窗口
/// 3. 同浏览器多个 Profile / 多个扩展实例
/// 4. 每个扩展独立上报 browserInfo
/// 5. WinSwitch 聚合所有客户端的窗口缓存
///
/// 设计：
/// - 每个浏览器扩展连接一个 NamedPipe 客户端
/// - 每个客户端维护自己的窗口快照
/// - BrowserWindows 为所有客户端窗口的聚合结果
/// - 快捷键触发时直接查本地缓存，不等待扩展响应
/// </summary>
public class BrowserBridgeService : IDisposable
{
    private const string PipeName = "WinSwitch.BrowserBridge";
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly object _syncRoot = new();

    /// <summary>
    /// 每个浏览器扩展客户端对应一份窗口快照
    /// key: clientId
    /// value: 客户端状态
    /// </summary>
    private readonly ConcurrentDictionary<string, BrowserClientState> _clients = new();

    /// <summary>
    /// 正在运行的客户端处理任务
    /// </summary>
    private readonly ConcurrentDictionary<string, Task> _clientTasks = new();

    /// <summary>
    /// 最新聚合后的所有浏览器窗口信息
    /// </summary>
    public List<BrowserWindowInfo> BrowserWindows { get; private set; } = new();

    /// <summary>
    /// 最后一次收到任意浏览器数据的时间
    /// </summary>
    public DateTime LastSyncTime { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// 浏览器数据更新事件
    /// </summary>
    public event Action? BrowserDataUpdated;

    /// <summary>
    /// 当前是否至少有一个浏览器扩展连接
    /// </summary>
    public bool IsConnected => _clients.Values.Any(c => c.IsConnected);

    /// <summary>
    /// 当前连接的浏览器扩展数量
    /// </summary>
    public int ConnectedClientCount => _clients.Values.Count(c => c.IsConnected);

    /// <summary>
    /// 缓存是否新鲜，5秒内同步过
    /// </summary>
    public bool IsCacheFresh => (DateTime.UtcNow - LastSyncTime).TotalSeconds < 5;

    /// <summary>
    /// 缓存是否可用，60秒内同步过
    /// </summary>
    public bool IsCacheUsable => (DateTime.UtcNow - LastSyncTime).TotalSeconds < 60;

    /// <summary>
    /// 是否有浏览器数据
    /// </summary>
    public bool HasData => BrowserWindows.Count > 0;

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        LogService.Instance.Info("BrowserBridge 多客户端服务已启动，等待浏览器扩展连接...");
    }

    /// <summary>
    /// 持续接受多个浏览器扩展连接
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                var clientId = Guid.NewGuid().ToString("N");
                var state = new BrowserClientState
                {
                    ClientId = clientId,
                    Pipe = pipe,
                    ConnectedAt = DateTime.UtcNow,
                    LastSyncTime = DateTime.MinValue,
                    IsConnected = true
                };
                _clients[clientId] = state;
                LogService.Instance.Info($"浏览器扩展已连接，clientId={clientId}，当前连接数={ConnectedClientCount}");

                var task = Task.Run(() => HandleClientAsync(state, ct), ct);
                _clientTasks[clientId] = task;

                // pipe 交给 HandleClientAsync 处理，这里不要 Dispose
                pipe = null;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogService.Instance.Info($"BrowserBridge 接受连接异常: {ex.Message}，3秒后继续监听...");
                try
                {
                    if (!ct.IsCancellationRequested)
                        await Task.Delay(3000, ct);
                }
                catch { /* ignore */ }
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    /// <summary>
    /// 处理单个浏览器扩展客户端
    /// </summary>
    private async Task HandleClientAsync(BrowserClientState state, CancellationToken ct)
    {
        try
        {
            var pipe = state.Pipe;
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(pipe, ct);
                if (message == null) break;
                ProcessMessage(state, message);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            LogService.Instance.Info($"BrowserBridge 客户端异常，clientId={state.ClientId}: {ex.Message}");
        }
        finally
        {
            state.IsConnected = false;
            try { state.Pipe.Dispose(); } catch { /* ignore */ }
            _clientTasks.TryRemove(state.ClientId, out _);
            LogService.Instance.Info($"浏览器扩展已断开，clientId={state.ClientId}，当前连接数={ConnectedClientCount}");
        }
    }

    /// <summary>
    /// 读取带长度头的消息
    /// 协议：前 4 字节 UInt32 消息长度，后续 UTF-8 JSON
    /// </summary>
    private static async Task<string?> ReadMessageAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            var lengthBytes = new byte[4];
            var read = await ReadExactAsync(pipe, lengthBytes, 0, 4, ct);
            if (read < 4) return null;

            var length = BitConverter.ToUInt32(lengthBytes, 0);
            if (length == 0 || length > 10 * 1024 * 1024) return null;

            var msgBytes = new byte[length];
            var totalRead = await ReadExactAsync(pipe, msgBytes, 0, (int)length, ct);
            if (totalRead < length) return null;

            return System.Text.Encoding.UTF8.GetString(msgBytes);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int> ReadExactAsync(
        Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    private void ProcessMessage(BrowserClientState state, string json)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<BrowserBridgeMessage>(json);
            if (msg == null) return;
            if (msg.Type != "browserInfo") return;
            if (msg.Windows == null) return;

            var now = DateTime.UtcNow;

            state.SourceBrowser = msg.Browser ?? state.SourceBrowser ?? "unknown";
            state.SourceProfile = msg.Profile ?? state.SourceProfile;
            state.SourceInstanceId = msg.InstanceId ?? state.SourceInstanceId ?? state.ClientId;
            state.LastSyncTime = now;
            state.Windows = msg.Windows;
            state.IsConnected = true;

            // 给每个浏览器窗口补充来源信息，便于调试和去重
            foreach (var bw in state.Windows)
            {
                bw.SourceClientId = state.ClientId;
                bw.SourceBrowser = state.SourceBrowser;
                bw.SourceProfile = state.SourceProfile;
                bw.SourceInstanceId = state.SourceInstanceId;
            }

            LastSyncTime = now;
            LogBrowserWindows(state);
            RebuildBrowserWindows();
            BrowserDataUpdated?.Invoke();

            LogService.Instance.Debug(
                $"收到浏览器数据: clientId={state.ClientId}, browser={state.SourceBrowser}, profile={state.SourceProfile}, windows={state.Windows.Count}, total={BrowserWindows.Count}");
        }
        catch (Exception ex)
        {
            LogService.Instance.Info($"处理浏览器数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 聚合所有客户端的浏览器窗口
    /// </summary>
    private void RebuildBrowserWindows()
    {
        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            // 清理120秒未同步的断开客户端缓存
            var expiredClientIds = _clients
                .Where(kv => !kv.Value.IsConnected && kv.Value.LastSyncTime != DateTime.MinValue && (now - kv.Value.LastSyncTime).TotalSeconds > 120)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var clientId in expiredClientIds)
            {
                _clients.TryRemove(clientId, out _);
                LogService.Instance.Debug($"已清理过期浏览器客户端缓存: clientId={clientId}");
            }

            BrowserWindows = _clients.Values
                .Where(c => c.Windows.Count > 0)
                .SelectMany(c => c.Windows)
                .ToList();
        }
    }

    private static void LogBrowserWindows(BrowserClientState state)
    {
        foreach (var bw in state.Windows)
        {
            LogService.Instance.Debug(
                $"浏览器窗口: clientId={state.ClientId}, browser={state.SourceBrowser}, profile={state.SourceProfile}, windowId={bw.BrowserWindowId}, tabs={bw.Tabs?.Count ?? 0}, focused={bw.Focused}, state={bw.State}, pos=({bw.Left},{bw.Top}), size={bw.Width}x{bw.Height}");
            if (bw.Tabs == null) continue;
            foreach (var tab in bw.Tabs)
            {
                LogService.Instance.Debug(
                    $"  标签页 {tab.TabId}: title='{tab.Title}', url='{tab.Url}', active={tab.Active}");
            }
        }
    }

    /// <summary>
    /// 查找匹配指定规则的浏览器窗口
    /// 直接查缓存，不请求扩展
    /// </summary>
    public List<BrowserWindowInfo> FindMatchingBrowserWindows(WindowRule rule)
    {
        List<BrowserWindowInfo> snapshot;
        lock (_syncRoot)
        {
            snapshot = BrowserWindows.ToList();
        }

        var result = new List<BrowserWindowInfo>();
        foreach (var bw in snapshot)
        {
            bool matched = rule.BrowserMatchMode switch
            {
                BrowserMatchMode.ActiveTabTitle => MatchActiveTab(bw, rule),
                BrowserMatchMode.AnyTabTitle => MatchAnyTabTitle(bw, rule),
                BrowserMatchMode.AnyTabUrl => MatchAnyTabUrl(bw, rule),
                _ => MatchActiveTab(bw, rule)
            };
            if (matched) result.Add(bw);
        }
        return result;
    }

    private static bool MatchActiveTab(BrowserWindowInfo bw, WindowRule rule)
    {
        if (bw.Tabs == null) return false;
        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
        if (activeTab == null) return false;

        // 浏览器扩展包含匹配模式：所有关键词都必须匹配
        if (rule.TitleMatchType == TitleMatchType.Contains)
        {
            var keywords = rule.TitlePattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // 所有关键词都必须在活动标签页标题中匹配
            return keywords.All(kw => WindowEnumerator.IsFullKeywordMatch(activeTab.Title, kw));
        }

        return WindowEnumerator.IsTitleMatch(activeTab.Title, rule.TitlePattern, rule.TitleMatchType);
    }

    private static bool MatchAnyTabTitle(BrowserWindowInfo bw, WindowRule rule)
    {
        if (bw.Tabs == null) return false;
        if (string.IsNullOrWhiteSpace(rule.TitlePattern)) return false;
        var keywords = rule.TitlePattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 浏览器扩展包含匹配模式：所有关键词都必须在窗口的标签页中匹配到
        if (rule.TitleMatchType == TitleMatchType.Contains)
        {
            // 每个关键词必须在至少一个标签页标题中匹配
            return keywords.All(kw => bw.Tabs.Any(tab => !string.IsNullOrEmpty(tab.Title) && WindowEnumerator.IsFullKeywordMatch(tab.Title, kw)));
        }

        // 非包含模式（Regex/StartsWith/Exact）按原逻辑
        return bw.Tabs.Any(tab => !string.IsNullOrEmpty(tab.Title) &&
            WindowEnumerator.IsTitleMatch(tab.Title, rule.TitlePattern, rule.TitleMatchType));
    }

    private static bool MatchAnyTabUrl(BrowserWindowInfo bw, WindowRule rule)
    {
        if (bw.Tabs == null) return false;
        if (string.IsNullOrEmpty(rule.UrlPattern)) return false;

        return bw.Tabs.Any(tab =>
        {
            if (string.IsNullOrEmpty(tab.Url)) return false;
            return rule.UrlMatchType switch
            {
                UrlMatchType.Contains => tab.Url.Contains(rule.UrlPattern, StringComparison.OrdinalIgnoreCase),
                UrlMatchType.StartsWith => tab.Url.StartsWith(rule.UrlPattern, StringComparison.OrdinalIgnoreCase),
                UrlMatchType.Exact => string.Equals(tab.Url, rule.UrlPattern, StringComparison.OrdinalIgnoreCase),
                UrlMatchType.Regex => System.Text.RegularExpressions.Regex.IsMatch(tab.Url, rule.UrlPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                _ => tab.Url.Contains(rule.UrlPattern, StringComparison.OrdinalIgnoreCase)
            };
        });
    }

    /// <summary>
    /// 将浏览器窗口与 Win32 HWND 关联
    /// 支持多浏览器、多窗口、一一对应
    /// 策略1：位置 + 宽高匹配，容差 ±30 像素
    /// 策略2：活动标签页标题匹配
    /// 策略3：唯一剩余窗口自动关联
    /// </summary>
    public void MatchBrowserWindowsToHwnd(List<WindowInfo> win32Windows)
    {
        if (win32Windows == null || win32Windows.Count == 0) return;

        List<BrowserWindowInfo> snapshot;
        lock (_syncRoot)
        {
            snapshot = BrowserWindows.ToList();
        }

        var browserProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "brave", "vivaldi", "opera", "firefox"
        };

        var availableWindows = win32Windows
            .Where(w => browserProcessNames.Contains(w.ProcessName))
            .ToList();

        var usedHwnds = new HashSet<IntPtr>();

        foreach (var bw in snapshot)
        {
            if (bw.MatchedHwnd != IntPtr.Zero)
            {
                usedHwnds.Add(bw.MatchedHwnd);
                continue;
            }

            // 策略1：非最小化窗口，用位置 + 宽高匹配
            if (!string.Equals(bw.State, "minimized", StringComparison.OrdinalIgnoreCase))
            {
                var posMatched = availableWindows.FirstOrDefault(w =>
                    !usedHwnds.Contains(w.Handle) &&
                    Math.Abs(w.Left - bw.Left) <= 30 &&
                    Math.Abs(w.Top - bw.Top) <= 30 &&
                    Math.Abs(w.Width - bw.Width) <= 30 &&
                    Math.Abs(w.Height - bw.Height) <= 30);
                if (posMatched != null)
                {
                    bw.MatchedHwnd = posMatched.Handle;
                    usedHwnds.Add(posMatched.Handle);
                    LogService.Instance.Debug($"HWND匹配[位置]: browser={bw.SourceBrowser}, windowId={bw.BrowserWindowId} -> HWND {posMatched.Handle}");
                    continue;
                }
            }

            // 策略2：活动标签页标题匹配
            var activeTab = bw.Tabs?.FirstOrDefault(t => t.Active);
            if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
            {
                var titleMatched = availableWindows.FirstOrDefault(w =>
                    !usedHwnds.Contains(w.Handle) &&
                    !string.IsNullOrEmpty(w.Title) &&
                    w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase));
                if (titleMatched != null)
                {
                    bw.MatchedHwnd = titleMatched.Handle;
                    usedHwnds.Add(titleMatched.Handle);
                    LogService.Instance.Debug($"HWND匹配[标题]: browser={bw.SourceBrowser}, windowId={bw.BrowserWindowId}, title={activeTab.Title} -> HWND {titleMatched.Handle}");
                    continue;
                }
            }

            // 策略3：如果只剩一个 Win32 浏览器窗口，则自动关联
            var remaining = availableWindows.Where(w => !usedHwnds.Contains(w.Handle)).ToList();
            if (remaining.Count == 1)
            {
                bw.MatchedHwnd = remaining[0].Handle;
                usedHwnds.Add(remaining[0].Handle);
                LogService.Instance.Debug($"HWND匹配[唯一剩余]: browser={bw.SourceBrowser}, windowId={bw.BrowserWindowId} -> HWND {remaining[0].Handle}");
                continue;
            }

            LogService.Instance.Debug($"HWND匹配失败: browser={bw.SourceBrowser}, windowId={bw.BrowserWindowId}, state={bw.State}, pos=({bw.Left},{bw.Top}), size={bw.Width}x{bw.Height}");
        }

        lock (_syncRoot)
        {
            BrowserWindows = snapshot;
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _acceptLoopTask?.Wait(1000); } catch { /* ignore */ }
        foreach (var client in _clients.Values)
        {
            try { client.Pipe.Dispose(); } catch { /* ignore */ }
        }
        _clients.Clear();
        _clientTasks.Clear();
        _cts?.Dispose();
        _cts = null;
    }

    private class BrowserClientState
    {
        public string ClientId { get; set; } = string.Empty;
        public NamedPipeServerStream Pipe { get; set; } = null!;
        public bool IsConnected { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastSyncTime { get; set; }
        public string? SourceBrowser { get; set; }
        public string? SourceProfile { get; set; }
        public string? SourceInstanceId { get; set; }
        public List<BrowserWindowInfo> Windows { get; set; } = new();
    }

    private class BrowserBridgeMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("browser")]
        public string? Browser { get; set; }

        [JsonProperty("profile")]
        public string? Profile { get; set; }

        [JsonProperty("instanceId")]
        public string? InstanceId { get; set; }

        [JsonProperty("windows")]
        public List<BrowserWindowInfo>? Windows { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }
}
