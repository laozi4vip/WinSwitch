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
    /// </summary>
    private void Hide()
    {
        var config = _configService.Config;
        var enabledRules = config.Rules.Where(r => r.BossKeyEnabled).ToList();

        foreach (var rule in enabledRules)
        {
            var hWnd = _enumerator.FindTargetWindow(rule);
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) continue;

            // 缓存当前窗口扩展样式
            rule.CachedExStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();
            rule.IsBossKeyHidden = true;

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
        var hiddenRules = config.Rules.Where(r => r.IsBossKeyHidden).ToList();

        foreach (var rule in hiddenRules)
        {
            var hWnd = _enumerator.FindTargetWindow(rule);
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
            {
                rule.IsBossKeyHidden = false;
                continue;
            }

            switch (config.BossKeyMode)
            {
                case BossKeyMode.HideWindowOnly:
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
                case BossKeyMode.HideWindowAndTaskbar:
                    RestoreExStyle(hWnd, rule.CachedExStyle);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
                case BossKeyMode.HideWindowAndTaskbarAndAltTab:
                    RemoveToolWindowStyle(hWnd);
                    RestoreExStyle(hWnd, rule.CachedExStyle);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
            }

            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
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

    private static void RestoreExStyle(IntPtr hWnd, int cachedExStyle)
    {
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, (IntPtr)cachedExStyle);
    }
}
