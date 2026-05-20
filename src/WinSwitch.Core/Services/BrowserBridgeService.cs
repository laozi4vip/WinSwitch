using System.IO.Pipes;
using Newtonsoft.Json;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 浏览器桥接服务 — 通过命名管道接收浏览器扩展发送的窗口/标签页信息
/// </summary>
public class BrowserBridgeService : IDisposable
{
    private const string PipeName = "WinSwitch.BrowserBridge";
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// 最新接收到的浏览器窗口信息
    /// </summary>
    public List<BrowserWindowInfo> BrowserWindows { get; private set; } = new();

    /// <summary>
    /// 浏览器数据更新事件
    /// </summary>
    public event Action? BrowserDataUpdated;

    /// <summary>
    /// 是否已连接到浏览器扩展
    /// </summary>
    public bool IsConnected => _pipeServer?.IsConnected == true;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
        LogService.Instance.Info("BrowserBridge 服务已启动，等待浏览器扩展连接...");
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

                // 等待 Native Host 连接
                await _pipeServer.WaitForConnectionAsync(ct);
                LogService.Instance.Info("浏览器扩展已连接");

                // 持续读取消息
                while (_pipeServer.IsConnected && !ct.IsCancellationRequested)
                {
                    var message = await ReadMessageAsync(_pipeServer, ct);
                    if (message == null) break;

                    ProcessMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogService.Instance.Info($"BrowserBridge 连接异常: {ex.Message}，3秒后重连...");
                await Task.Delay(3000, ct);
            }
            finally
            {
                _pipeServer?.Dispose();
                _pipeServer = null;
            }
        }
    }

    /// <summary>
    /// 从管道读取消息（4字节长度前缀 + JSON）
    /// </summary>
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 处理接收到的浏览器数据
    /// </summary>
    private void ProcessMessage(string json)
    {
        try
        {
            var msg = JsonConvert.DeserializeObject<BrowserBridgeMessage>(json);
            if (msg?.Type == "browserInfo" && msg.Windows != null)
            {
                BrowserWindows = msg.Windows;
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
    /// 查找匹配指定规则的浏览器窗口
    /// 支持三种匹配模式：当前标签页标题、任意标签页标题、任意标签页URL
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

            if (matched)
            {
                result.Add(bw);
            }
        }

        return result;
    }

    /// <summary>
    /// 方式一：匹配当前活动标签页标题
    /// </summary>
    private static bool MatchActiveTab(BrowserWindowInfo bw, WindowRule rule)
    {
        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
        if (activeTab == null) return false;

        return WindowEnumerator.IsTitleMatch(activeTab.Title, rule.TitlePattern, rule.TitleMatchType);
    }

    /// <summary>
    /// 方式二：匹配任意标签页标题
    /// </summary>
    private static bool MatchAnyTabTitle(BrowserWindowInfo bw, WindowRule rule)
    {
        return bw.Tabs.Any(tab => WindowEnumerator.IsTitleMatch(tab.Title, rule.TitlePattern, rule.TitleMatchType));
    }

    /// <summary>
    /// 方式三：匹配任意标签页 URL
    /// </summary>
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
    /// 将浏览器窗口与 Win32 HWND 关联（通过窗口位置匹配）
    /// </summary>
    public void MatchBrowserWindowsToHwnd(List<WindowInfo> win32Windows)
    {
        foreach (var bw in BrowserWindows)
        {
            // 通过窗口位置匹配（容差±10像素）
            var matched = win32Windows.FirstOrDefault(w =>
                Math.Abs(w.Left - bw.Left) <= 10 &&
                Math.Abs(w.Top - bw.Top) <= 10 &&
                Math.Abs((w.ExStyle & 0) - 0) >= 0); // 简单匹配

            if (matched != null)
            {
                bw.MatchedHwnd = matched.Handle;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _pipeServer?.Dispose();
        _cts?.Dispose();
    }

    /// <summary>
    /// 浏览器桥接消息格式
    /// </summary>
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
