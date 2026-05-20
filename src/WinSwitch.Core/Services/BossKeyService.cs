using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 老板键服务 — 实现需求 §6.2 三种隐藏模式
/// V2: 支持浏览器扩展匹配模式
/// </summary>
public class BossKeyService
{
    private readonly WindowEnumerator _enumerator;
    private readonly ConfigService _configService;
    private readonly BrowserBridgeService? _browserBridge;

    /// <summary>
    /// 记录被老板键隐藏的窗口句柄列表，用于恢复
    /// </summary>
    private readonly List<IntPtr> _hiddenWindowHandles = new();

    public BossKeyService(WindowEnumerator enumerator, ConfigService configService, BrowserBridgeService? browserBridge = null)
    {
        _enumerator = enumerator;
        _configService = configService;
        _browserBridge = browserBridge;
    }

    /// <summary>
    /// 老板键事件
    /// </summary>
    public event Action<bool>? BossKeyToggled;

    /// <summary>
    /// 当前是否处于老板键隐藏状态
    /// </summary>
    public bool IsHidden { get; private set; }

    /// <summary>
    /// 执行老板键操作（切换隐藏/恢复）
    /// </summary>
    public void Toggle()
    {
        if (IsHidden)
        {
            Restore();
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// 隐藏所有 bossKeyEnabled=true 的窗口
    /// V2: 有浏览器扩展数据时，也走扩展路径匹配浏览器窗口
    /// </summary>
    private void Hide()
    {
        var config = _configService.Config;
        var enabledRules = config.Rules.Where(r => r.BossKeyEnabled).ToList();
        _hiddenWindowHandles.Clear();

        // 收集所有需要隐藏的 HWND（去重）
        var allHandlesToHide = new HashSet<IntPtr>();

        foreach (var rule in enabledRules)
        {
            var matchingHandles = new List<IntPtr>();

            // 1. Win32 模式匹配
            var win32Handles = _enumerator.FindAllMatchingWindows(rule);
            matchingHandles.AddRange(win32Handles);

            // 2. V2: 浏览器扩展匹配（如果有浏览器数据且规则使用扩展模式）
            if (_browserBridge != null && _browserBridge.HasData)
            {
                var browserWindows = _browserBridge.FindMatchingBrowserWindows(rule);
                if (browserWindows.Count > 0)
                {
                    var win32Windows = _enumerator.EnumerateAllWindows();
                    _browserBridge.MatchBrowserWindowsToHwnd(win32Windows);

                    foreach (var bw in browserWindows)
                    {
                        if (bw.MatchedHwnd != IntPtr.Zero)
                        {
                            matchingHandles.Add(bw.MatchedHwnd);
                        }
                        else
                        {
                            // HWND匹配失败，用活动标签页标题查找
                            var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
                            if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
                            {
                                var winByTitle = win32Windows.FirstOrDefault(w =>
                                    w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase) &&
                                    IsBrowserProcess(w.ProcessName));
                                if (winByTitle != null && winByTitle.Handle != IntPtr.Zero)
                                {
                                    matchingHandles.Add(winByTitle.Handle);
                                }
                            }
                        }
                    }
                }
            }

            // 去重并执行隐藏
            foreach (var hWnd in matchingHandles)
            {
                if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) continue;
                if (allHandlesToHide.Contains(hWnd)) continue;

                switch (config.BossKeyMode)
                {
                    case BossKeyMode.HideWindowOnly:
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                        break;
                    case BossKeyMode.HideWindowAndTaskbar:
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                        RemoveTaskbarIcon(hWnd);
                        break;
                    case BossKeyMode.HideWindowAndTaskbarAndAltTab:
                        AddToolWindowStyle(hWnd);
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                        RemoveTaskbarIcon(hWnd);
                        break;
                }

                _hiddenWindowHandles.Add(hWnd);
                allHandlesToHide.Add(hWnd);
            }

            rule.IsBossKeyHidden = matchingHandles.Count > 0;
            rule.CachedExStyle = matchingHandles.Count > 0 
                ? NativeMethods.GetWindowLongPtr(matchingHandles[0], NativeMethods.GWL_EXSTYLE).ToInt32() 
                : 0;
        }

        IsHidden = true;
        BossKeyToggled?.Invoke(true);
    }

    /// <summary>
    /// 恢复所有被老板键隐藏的窗口
    /// </summary>
    private void Restore()
    {
        var config = _configService.Config;

        foreach (var hWnd in _hiddenWindowHandles)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) continue;

            switch (config.BossKeyMode)
            {
                case BossKeyMode.HideWindowOnly:
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
                case BossKeyMode.HideWindowAndTaskbar:
                    RestoreAppWindowStyle(hWnd);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
                case BossKeyMode.HideWindowAndTaskbarAndAltTab:
                    RemoveToolWindowStyle(hWnd);
                    RestoreAppWindowStyle(hWnd);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
            }

            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        }

        _hiddenWindowHandles.Clear();

        foreach (var rule in config.Rules.Where(r => r.IsBossKeyHidden))
        {
            rule.IsBossKeyHidden = false;
            rule.CachedExStyle = 0;
        }

        IsHidden = false;
        BossKeyToggled?.Invoke(false);
    }

    private static bool IsBrowserProcess(string processName)
    {
        return processName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("firefox", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveTaskbarIcon(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    private static void RestoreAppWindowStyle(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle |= NativeMethods.WS_EX_APPWINDOW;
        exStyle &= ~NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    private static void AddToolWindowStyle(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    private static void RemoveToolWindowStyle(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();
        exStyle &= ~NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }
}
