using System.IO.Pipes;
using Newtonsoft.Json;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 浏览器桥接服务 - 事件驱动同步 + 本地缓存 + 快捷键直接查缓存
/// 扩展平时维护浏览器状态 → 变化时同步给 WinSwitch → WinSwitch 保存缓存
/// 快捷键触发时只查缓存,不等待扩展,确保响应无延迟
/// </summary>
public class BrowserBridgeService : IDisposable
{
    private const string PipeName = "WinSwitch.BrowserBridge";
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// 最新接收到的浏览器窗口信息(本地缓存)
    /// </summary>
    public List<BrowserWindowInfo> BrowserWindows { get; private set; } = new();

    /// <summary>
    /// 最后一次收到浏览器数据的时间
    /// </summary>
    public DateTime LastSyncTime { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// 浏览器数据更新事件
    /// </summary>
    public event Action? BrowserDataUpdated;

    /// <summary>
    /// 是否已连接到浏览器扩展
    /// </summary>
    public bool IsConnected => _pipeServer?.IsConnected == true;

    /// <summary>
    /// 缓存是否新鲜(5秒内同步过)
    /// </summary>
    public bool IsCacheFresh => (DateTime.UtcNow - LastSyncTime).TotalSeconds < 5;

    /// <summary>
    /// 缓存是否可用(60秒内同步过)
    /// </summary>
    public bool IsCacheUsable => (DateTime.UtcNow - LastSyncTime).TotalSeconds < 60;

    /// <summary>
    /// 是否有浏览器数据
    /// </summary>
    public bool HasData => BrowserWindows.Count > 0;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
        LogService.Instance.Info("BrowserBridge 服务已启动,等待浏览器扩展连接...");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _pipeServer = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await _pipeServer.WaitForConnectionAsync(ct);
                LogService.Instance.Info("浏览器扩展已连接");

                while (_pipeServer.IsConnected && !ct.IsCancellationRequested)
                {
                    var message = await ReadMessageAsync(_pipeServer, ct);
                    if (message == null) break;
                    ProcessMessage(message);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogService.Instance.Info($"BrowserBridge 连接异常: {ex.Message},3秒后重连...");
                await Task.Delay(3000, ct);
            }
            finally
            {
                _pipeServer?.Dispose();
                _pipeServer = null;
            }
        }
    }

    private static async Task<string?> ReadMessageAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            var lengthBytes = new byte[4];
            var read = await pipe.ReadAsync(lengthBytes, 0, 4, ct);
            if (read < 4) return null;
            var length = BitConverter.ToUInt32(lengthBytes, 0);
            if (length == 0 || length > 10 * 1024 * 1024) return null;
            var msgBytes = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                var r = await pipe.ReadAsync(msgBytes, totalRead, (int)length - totalRead, ct);
                if (r == 0) return null;
                totalRead += r;
            }
            return System.Text.Encoding.UTF8.GetString(msgBytes);
        }
        catch { return null; }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<BrowserBridgeMessage>(json);
            if (msg?.Type == "browserInfo" && msg.Windows != null)
            {
                BrowserWindows = msg.Windows;
                LastSyncTime = DateTime.UtcNow;
                // 详细日志:每个窗口的标签页数量
                foreach (var bw in BrowserWindows)
                {
                    LogService.Instance.Debug($"浏览器窗口 {bw.BrowserWindowId}: {bw.Tabs?.Count ?? 0} 个标签页, 焦点={bw.Focused}, 位置=({bw.Left},{bw.Top}), 大小={bw.Width}x{bw.Height}");
                    if (bw.Tabs != null)
                    {
                        foreach (var tab in bw.Tabs)
                        {
                            LogService.Instance.Debug($"  标签页 {tab.TabId}: title='{tab.Title}', url='{tab.Url}', active={tab.Active}");
                        }
                    }
                }
                BrowserDataUpdated?.Invoke();
                LogService.Instance.Debug($"收到浏览器数据: {BrowserWindows.Count} 个窗口");
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Info($"处理浏览器数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 查找匹配指定规则的浏览器窗口(直接查缓存,不请求扩展)
    /// </summary>
    public List<BrowserWindowInfo> FindMatchingBrowserWindows(WindowRule rule)
    {
        var result = new List<BrowserWindowInfo>();
        foreach (var bw in BrowserWindows)
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
        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
        if (activeTab == null) return false;
        return WindowEnumerator.IsTitleMatch(activeTab.Title, rule.TitlePattern, rule.TitleMatchType);
    }

    private static bool MatchAnyTabTitle(BrowserWindowInfo bw, WindowRule rule)
    {
        // 任意标签页标题匹配
        // 单关键词:任意标签页命中即匹配(与 ActiveTabTitle 不同的是扫描所有标签页而非仅活动标签页)
        // 多关键词(分号分隔):窗口中至少需要匹配2个不同的标签页
        var keywords = rule.TitlePattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (keywords.Length <= 1)
        {
            // 单关键词:任意标签页命中即可
            return bw.Tabs.Any(tab => !string.IsNullOrEmpty(tab.Title) &&
                WindowEnumerator.IsTitleMatch(tab.Title, rule.TitlePattern, rule.TitleMatchType));
        }

        // 多关键词:至少需要匹配2个不同的标签页
        int matchedCount = 0;
        foreach (var tab in bw.Tabs)
        {
            if (string.IsNullOrEmpty(tab.Title)) continue;
            bool tabMatched = keywords.Any(kw => tab.Title.Contains(kw, StringComparison.OrdinalIgnoreCase));
            if (tabMatched)
            {
                matchedCount++;
                if (matchedCount >= 2) return true;
            }
        }
        return false;
    }

    private static bool MatchAnyTabUrl(BrowserWindowInfo bw, WindowRule rule)
    {
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
    /// V2.1: 已匹配的 HWND 从候选列表移除，确保多窗口一一对应
    /// 策略1: 位置+宽高匹配（容差±30像素）
    /// 策略2: 活动标签页标题匹配
    /// 策略3: 单窗口自动关联
    /// </summary>
    public void MatchBrowserWindowsToHwnd(List<WindowInfo> win32Windows)
    {
        var browserProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "brave", "vivaldi", "opera", "firefox"
        };

        // 可用候选列表（匹配后移除，确保一对一）
        var availableWindows = win32Windows
            .Where(w => browserProcessNames.Contains(w.ProcessName))
            .ToList();

        // 已被占用的 HWND 集合
        var usedHwnds = new HashSet<IntPtr>();

        foreach (var bw in BrowserWindows)
        {
            if (bw.MatchedHwnd != IntPtr.Zero) continue;

            // 策略1: 非最小化窗口，用位置+宽高匹配（容差±30像素）
            if (bw.State != "minimized")
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
                    LogService.Instance.Debug($"HWND匹配(位置): 浏览器窗口{bw.BrowserWindowId} -> HWND {posMatched.Handle}");
                    continue;
                }
            }

            // 策略2: 用活动标签页标题匹配（排除已占用的HWND）
            var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
            if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
            {
                var titleMatched = availableWindows.FirstOrDefault(w =>
                    !usedHwnds.Contains(w.Handle) &&
                    w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase));
                if (titleMatched != null)
                {
                    bw.MatchedHwnd = titleMatched.Handle;
                    usedHwnds.Add(titleMatched.Handle);
                    LogService.Instance.Debug($"HWND匹配(标题): 浏览器窗口{bw.BrowserWindowId} -> HWND {titleMatched.Handle}");
                    continue;
                }
            }

            // 策略3: 如果浏览器窗口数 == Win32浏览器窗口数，按顺序关联剩余的
            var remaining = availableWindows.Where(w => !usedHwnds.Contains(w.Handle)).ToList();
            if (remaining.Count == 1)
            {
                bw.MatchedHwnd = remaining[0].Handle;
                usedHwnds.Add(remaining[0].Handle);
                LogService.Instance.Debug($"HWND匹配(唯一剩余): 浏览器窗口{bw.BrowserWindowId} -> HWND {remaining[0].Handle}");
                continue;
            }

            LogService.Instance.Debug($"HWND匹配失败: 浏览器窗口{bw.BrowserWindowId}, state={bw.State}, pos=({bw.Left},{bw.Top}), size={bw.Width}x{bw.Height}");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _pipeServer?.Dispose();
        _cts?.Dispose();
    }

    private class BrowserBridgeMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
        [JsonProperty("windows")]
        public List<BrowserWindowInfo>? Windows { get; set; }
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }
    }
}
