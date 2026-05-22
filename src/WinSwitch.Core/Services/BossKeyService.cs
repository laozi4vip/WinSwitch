using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 老板键服务 — 实现需求 §6.2 三种隐藏模式
/// v2.1.1: 统一使用程序名匹配，按进程名隐藏该进程所有窗口
/// </summary>
public class BossKeyService
{
    private readonly WindowEnumerator _enumerator;
    private readonly ConfigService _configService;

    /// <summary>
    /// 记录被老板键隐藏的窗口句柄列表，用于恢复
    /// </summary>
    private readonly List<IntPtr> _hiddenWindowHandles = new();

    public BossKeyService(WindowEnumerator enumerator, ConfigService configService)
    {
        _enumerator = enumerator;
        _configService = configService;
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
    /// v2.1.1: 统一程序名匹配 — 直接按进程名枚举所有窗口并隐藏
    /// 不再使用标题匹配或浏览器扩展匹配，彻底避免跨规则误伤
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
            // v2.1.1: 纯程序名匹配 — 找到该进程的所有窗口
            var windows = _enumerator.FindWindowsByProcess(rule.ProcessName);

            foreach (var win in windows)
            {
                var hWnd = win.Handle;
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

            rule.IsBossKeyHidden = windows.Count > 0;
            rule.CachedExStyle = windows.Count > 0
                ? NativeMethods.GetWindowLongPtr(windows[0].Handle, NativeMethods.GWL_EXSTYLE).ToInt32()
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
