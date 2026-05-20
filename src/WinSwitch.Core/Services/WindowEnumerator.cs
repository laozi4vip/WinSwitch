using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 窗口枚举与查找服务
/// </summary>
public class WindowEnumerator
{
    /// <summary>
    /// 枚举所有可见顶层窗口
    /// </summary>
    public List<WindowInfo> EnumerateAllWindows()
    {
        var windows = new List<WindowInfo>();
        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            var titleLength = NativeMethods.GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return true;

            var titleBuilder = new StringBuilder(titleLength + 1);
            NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString();

            NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);

            string processName;
            try
            {
                var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch
            {
                processName = "Unknown";
            }

            var exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt32();

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                ProcessName = processName,
                Title = title,
                ProcessId = (int)processId,
                IsVisible = true,
                IsTopLevel = true,
                ExStyle = exStyle
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// 按进程名查找所有可见窗口（浏览器等有多窗口进程需要）
    /// </summary>
    public List<WindowInfo> FindAllWindowsByProcess(string processName)
    {
        return EnumerateAllWindows()
            .Where(w => string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// 按规则查找目标窗口
    /// </summary>
    public IntPtr FindTargetWindow(WindowRule rule)
    {
        if (rule.MatchMode == MatchMode.Fixed)
        {
            return FindFixedWindow(rule);
        }
        else
        {
            return FindRuleWindow(rule);
        }
    }

    /// <summary>
    /// 查找同一进程的所有窗口
    /// </summary>
    public List<WindowInfo> FindWindowsByProcess(string processName)
    {
        return EnumerateAllWindows()
            .Where(w => string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Fixed 模式：先检查缓存句柄，无效则按进程名重定位
    /// </summary>
    private IntPtr FindFixedWindow(WindowRule rule)
    {
        // 检查缓存句柄是否有效
        if (rule.CachedHandle != IntPtr.Zero && NativeMethods.IsWindow(rule.CachedHandle))
        {
            return rule.CachedHandle;
        }

        // 句柄失效，按进程名重新枚举
        var windows = FindWindowsByProcess(rule.ProcessName);
        if (windows.Count > 0)
        {
            rule.CachedHandle = windows[0].Handle;
            return windows[0].Handle;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Rule 模式：按进程名+标题规则匹配
    /// </summary>
    private IntPtr FindRuleWindow(WindowRule rule)
    {
        var windows = FindWindowsByProcess(rule.ProcessName);

        foreach (var window in windows)
        {
            if (IsTitleMatch(window.Title, rule.TitlePattern, rule.TitleMatchType))
            {
                return window.Handle;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 标题匹配
    /// </summary>
    public static bool IsTitleMatch(string title, string pattern, TitleMatchType matchType)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        return matchType switch
        {
            TitleMatchType.Contains => title.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatchType.StartsWith => title.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatchType.Exact => string.Equals(title, pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatchType.Regex => Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase),
            _ => false
        };
    }

    /// <summary>
    /// 获取当前前台窗口
    /// </summary>
    public IntPtr GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    /// <summary>
    /// 检查窗口是否为前台窗口
    /// </summary>
    public bool IsForegroundWindow(IntPtr hWnd)
    {
        return NativeMethods.GetForegroundWindow() == hWnd;
    }
}
