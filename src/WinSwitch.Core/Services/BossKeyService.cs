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
    /// </summary>
    private void Hide()
    {
        var config = _configService.Config;
        var enabledRules = config.Rules.Where(r => r.BossKeyEnabled).ToList();

        foreach (var rule in enabledRules)
        {
            var hWnd = _enumerator.FindTargetWindow(rule);
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
                continue;

            // 缓存当前窗口扩展样式
            rule.CachedExStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            rule.IsBossKeyHidden = true;

            switch (config.BossKeyMode)
            {
                case BossKeyMode.HideWindowOnly:
                    // 模式1: 仅隐藏窗口
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                    break;

                case BossKeyMode.HideWindowAndTaskbar:
                    // 模式2: 隐藏窗口 + 移除任务栏图标
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
                    RemoveTaskbarIcon(hWnd);
                    break;

                case BossKeyMode.HideWindowAndTaskbarAndAltTab:
                    // 模式3: 隐藏窗口 + 移除任务栏图标 + 从 Alt+Tab 隐藏
                    // 先修改扩展样式（窗口可见时修改才生效）
                    AddToolWindowStyle(hWnd);
                    // 再隐藏窗口 + 移除任务栏
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
                    // 模式1 恢复: 显示窗口
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;

                case BossKeyMode.HideWindowAndTaskbar:
                    // 模式2 恢复: 恢复任务栏图标 + 显示窗口
                    RestoreExStyle(hWnd, rule.CachedExStyle);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;

                case BossKeyMode.HideWindowAndTaskbarAndAltTab:
                    // 模式3 恢复: 移除 ToolWindow 样式 + 恢复原始样式 + 显示窗口
                    RemoveToolWindowStyle(hWnd);
                    RestoreExStyle(hWnd, rule.CachedExStyle);
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    break;
            }

            // 激活窗口
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);

            rule.IsBossKeyHidden = false;
            rule.CachedExStyle = 0;
        }

        IsHidden = false;
        BossKeyToggled?.Invoke(false);
    }

    /// <summary>
    /// 移除任务栏图标：清除 WS_EX_APPWINDOW 标志
    /// </summary>
    private static void RemoveTaskbarIcon(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 添加 WS_EX_TOOLWINDOW 样式（从 Alt+Tab 隐藏）
    /// </summary>
    private static void AddToolWindowStyle(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 移除 WS_EX_TOOLWINDOW 样式
    /// </summary>
    private static void RemoveToolWindowStyle(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// 恢复缓存的原始扩展样式
    /// </summary>
    private static void RestoreExStyle(IntPtr hWnd, int cachedExStyle)
    {
        NativeMethods.SetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE, cachedExStyle);
    }
}
