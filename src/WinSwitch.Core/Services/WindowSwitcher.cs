using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 窗口切换服务 — 核心快捷键切换逻辑
/// 实现需求 §6.1：非前台→激活 / 前台→最小化
/// </summary>
public class WindowSwitcher
{
    private readonly WindowEnumerator _enumerator;

    public WindowSwitcher(WindowEnumerator enumerator)
    {
        _enumerator = enumerator;
    }

    /// <summary>
    /// 切换事件 — 通知 UI 层（成功/失败/未找到）
    /// </summary>
    public event Action<SwitchResult>? SwitchCompleted;

    /// <summary>
    /// 执行快捷键切换
    /// </summary>
    public SwitchResult Switch(WindowRule rule)
    {
        // TaskbarPin 模式：FindTargetWindow 已模拟 Win+数字键
        // 如果窗口切换了（前台变了），说明用户想激活它，不要立刻最小化
        // 如果窗口没变（目标已在前台），则可以最小化
        if (rule.MatchMode == MatchMode.TaskbarPin)
        {
            var hWnd = _enumerator.FindTargetWindow(rule);
            if (hWnd == IntPtr.Zero)
            {
                SwitchCompleted?.Invoke(SwitchResult.NotFound(rule));
                return SwitchResult.NotFound(rule);
            }
            // 前台窗口发生了切换 → 刚被激活，不做最小化
            if (_enumerator.IsForegroundWindow(hWnd))
            {
                SwitchCompleted?.Invoke(SwitchResult.Activated(rule));
                return SwitchResult.Activated(rule);
            }
            // 前台窗口没变 → 目标已在前台，执行最小化
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
            SwitchCompleted?.Invoke(SwitchResult.Minimized(rule));
            return SwitchResult.Minimized(rule);
        }

        var hWnd2 = _enumerator.FindTargetWindow(rule);

        if (hWnd2 == IntPtr.Zero)
        {
            SwitchCompleted?.Invoke(SwitchResult.NotFound(rule));
            return SwitchResult.NotFound(rule);
        }

        if (_enumerator.IsForegroundWindow(hWnd2))
        {
            // 前台窗口 → 最小化
            NativeMethods.ShowWindow(hWnd2, NativeMethods.SW_MINIMIZE);
            SwitchCompleted?.Invoke(SwitchResult.Minimized(rule));
            return SwitchResult.Minimized(rule);
        }
        else
        {
            // 非前台窗口 → 多级激活策略
            return ActivateWindow(hWnd2, rule);
        }
    }

    /// <summary>
    /// 多级激活策略（§6.1）
    /// </summary>
    private SwitchResult ActivateWindow(IntPtr hWnd, WindowRule rule)
    {
        // 尝试1：直接 SetForegroundWindow
        if (NativeMethods.SetForegroundWindow(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            SwitchCompleted?.Invoke(SwitchResult.Activated(rule));
            return SwitchResult.Activated(rule);
        }

        // 尝试2：AttachThreadInput + SetForegroundWindow
        var foregroundHandle = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(foregroundHandle, out var foregroundThread);
        NativeMethods.GetWindowThreadProcessId(hWnd, out var targetThread);

        if (foregroundThread != targetThread && foregroundThread != 0 && targetThread != 0)
        {
            NativeMethods.AttachThreadInput(foregroundThread, targetThread, true);
            var result = NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.AttachThreadInput(foregroundThread, targetThread, false);

            if (result)
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                SwitchCompleted?.Invoke(SwitchResult.Activated(rule));
                return SwitchResult.Activated(rule);
            }
        }

        // 尝试3：AllowSetForegroundWindow + SetForegroundWindow
        NativeMethods.AllowSetForegroundWindow(0xFFFFFFFF);
        if (NativeMethods.SetForegroundWindow(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            SwitchCompleted?.Invoke(SwitchResult.Activated(rule));
            return SwitchResult.Activated(rule);
        }

        // 降级：强制置顶 + 显示
        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        SwitchCompleted?.Invoke(SwitchResult.ActivatedWithFallback(rule));
        return SwitchResult.ActivatedWithFallback(rule);
    }
}

/// <summary>
/// 切换结果
/// </summary>
public class SwitchResult
{
    public WindowRule Rule { get; set; }
    public SwitchAction Action { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }

    private SwitchResult(WindowRule rule, SwitchAction action, string message, bool success)
    {
        Rule = rule;
        Action = action;
        Message = message;
        Success = success;
    }

    public static SwitchResult Activated(WindowRule rule) =>
        new(rule, SwitchAction.Activated, $"已激活窗口: {rule.Name}", true);

    public static SwitchResult Minimized(WindowRule rule) =>
        new(rule, SwitchAction.Minimized, $"已最小化窗口: {rule.Name}", true);

    public static SwitchResult NotFound(WindowRule rule) =>
        new(rule, SwitchAction.NotFound, $"未找到匹配窗口: {rule.Name}", false);

    public static SwitchResult ActivatedWithFallback(WindowRule rule) =>
        new(rule, SwitchAction.ActivatedWithFallback, $"窗口已显示(降级激活): {rule.Name}", true);
}

public enum SwitchAction
{
    Activated,
    Minimized,
    NotFound,
    ActivatedWithFallback
}
