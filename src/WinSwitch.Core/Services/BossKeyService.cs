using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 老板键服务 — 实现需求 §6.2 三种隐藏模式
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
        if (IsHidden) { Restore(); } else { Hide(); }
    }

    /// <summary>
    /// 隐藏所有 bossKeyEnabled=true 的窗口
    /// 修复：枚举进程的所有窗口（如浏览器的多个标签页窗口）
    /// </summary>
    private void Hide()
    {
        var config = _configService.Config;
        var enabledRules = config.Rules.Where(r => r.BossKeyEnabled).ToList();
        _hiddenWindowHandles.Clear();

        foreach (var rule in enabledRules)
        {
            // 查找该进程所有匹配的窗口（支持标题关键词过滤+浏览器多标签页）
            var matchingHandles = _enumerator.FindAllMatchingWindows(rule);

            foreach (var hWnd in matchingHandles)
            {
                
                if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) continue;

                // 缓存当前窗口扩展样式
                var cachedExStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();

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

                // 记录隐藏的窗口信息，用于恢复
                _hiddenWindowHandles.Add(hWnd);
            }

            rule.IsBossKeyHidden = true;
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

        // 恢复所有隐藏的窗口句柄
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

        // 清除规则的隐藏标记
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
