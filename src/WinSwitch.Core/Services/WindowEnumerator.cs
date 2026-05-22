using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using WinSwitch.Core.Interop;
using System.Linq;
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
        return rule.MatchMode switch
        {
            MatchMode.Fixed => FindFixedWindow(rule),
            MatchMode.Rule => FindRuleWindow(rule),
            MatchMode.ProcessName => FindProcessNameWindow(rule),
            MatchMode.TaskbarPin => FindTaskbarPinWindow(rule),
            _ => FindRuleWindow(rule)
        };
    }

    /// <summary>
    /// ProcessName 模式：仅按进程名匹配窗口（无需标题规则），返回第一个匹配的可见窗口
    /// </summary>
    private IntPtr FindProcessNameWindow(WindowRule rule)
    {
        var windows = FindWindowsByProcess(rule.ProcessName);
        return windows.Count > 0 ? windows[0].Handle : IntPtr.Zero;
    }

    /// <summary>
    /// 查找同一进程的所有窗口
    /// </summary>
        /// <summary>
    /// TaskbarPin 模式：按任务栏固定程序序号匹配窗口
    /// 读取 Quick Launch User Pinned TaskBar 目录获取目标程序的进程名
    /// </summary>
    private IntPtr FindTaskbarPinWindow(WindowRule rule)
    {
        var taskbarPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        if (!Directory.Exists(taskbarPath))
        {
            LogService.Instance.Warning($"TaskbarPin: 任务栏固定目录不存在: {taskbarPath}");
            return IntPtr.Zero;
        }

        var lnkFiles = Directory.GetFiles(taskbarPath, "*.lnk")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rule.TaskbarSlot < 1 || rule.TaskbarSlot > lnkFiles.Count)
        {
            LogService.Instance.Warning($"TaskbarPin: 序号 {rule.TaskbarSlot} 超出范围 (1-{lnkFiles.Count})");
            return IntPtr.Zero;
        }

        var lnkFile = lnkFiles[rule.TaskbarSlot - 1];
        var processName = ResolveShortcutTarget(lnkFile);
        if (string.IsNullOrEmpty(processName))
        {
            LogService.Instance.Warning($"TaskbarPin: 无法解析快捷方式: {lnkFile}");
            return IntPtr.Zero;
        }

        LogService.Instance.Info($"TaskbarPin: 序号 {rule.TaskbarSlot} -> {processName}");

        var windows = FindWindowsByProcess(processName);
        if (windows.Count == 0)
        {
            var baseName = Path.GetFileNameWithoutExtension(processName);
            windows = FindWindowsByProcess(baseName);
        }

        return windows.FirstOrDefault()?.Handle ?? IntPtr.Zero;
    }

    /// <summary>
    /// 解析 .lnk 快捷方式的目标文件（使用 IShellLink COM 接口）
    /// </summary>
    private static string ResolveShortcutTarget(string lnkPath)
    {
        try
        {
            // IShellLinkW CLSID
            var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
            if (shellLinkType == null) return string.Empty;

            var shellLink = Activator.CreateInstance(shellLinkType);
            if (shellLink == null) return string.Empty;

            // IPersistFile
            var persistFile = (System.Runtime.InteropServices.ComTypes.IPersistFile)shellLink;
            persistFile.Load(lnkPath, 0);

            // IShellLinkW -> GetPath
            var getPathMethod = shellLinkType.GetMethod("GetPath",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (getPathMethod == null) return string.Empty;

            var sb = new System.Text.StringBuilder(260);
            var path = sb;
            var args = new object[] { path, 260, IntPtr.Zero, 0 };
            getPathMethod.Invoke(shellLink, args);

            // Release COM
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shellLink);

            return args[0]?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"解析快捷方式失败: {lnkPath}, {ex.Message}");
            return string.Empty;
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
    /// 按进程名+标题规则查找所有匹配窗口（用于老板键/浏览器多标签页）
    /// </summary>
    public List<IntPtr> FindAllMatchingWindows(WindowRule rule)
    {
        var result = new List<IntPtr>();

        // Fixed 模式：全量返回该进程窗口
        if (rule.MatchMode == MatchMode.Fixed)
        {
            result.AddRange(FindWindowsByProcess(rule.ProcessName).Select(w => w.Handle));
            return result;
        }

        // Rule / ProcessName 模式：需要标题过滤
        // 如果无标题规则，不通过 Win32 匹配（避免误伤同进程其他窗口）
        if (string.IsNullOrEmpty(rule.TitlePattern))
        {
            return result;
        }

        var windows = FindWindowsByProcess(rule.ProcessName);
        foreach (var window in windows)
        {
            if (IsTitleMatch(window.Title, rule.TitlePattern, rule.TitleMatchType))
            {
                result.Add(window.Handle);
            }
        }
        return result;
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
    /// 包含模式支持多个关键词，用分号分隔（如：Chrome;Edge;Firefox）
    /// </summary>
    public static bool IsTitleMatch(string title, string pattern, TitleMatchType matchType)
    {
        if (string.IsNullOrEmpty(pattern)) return true;

        return matchType switch
        {
            TitleMatchType.Contains => MatchContainsAny(title, pattern),
            TitleMatchType.StartsWith => title.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatchType.Exact => string.Equals(title, pattern, StringComparison.OrdinalIgnoreCase),
            TitleMatchType.Regex => Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase),
            _ => false
        };
    }

    /// <summary>
    /// 包含匹配：支持分号分隔的多个关键词，所有关键词都必须匹配才算命中
    /// 示例："Chrome;工作台" → 标题必须同时包含"Chrome"和"工作台"才匹配
    /// 支持中文关键词和英文词边界
    /// </summary>
    private static bool MatchContainsAny(string title, string pattern)
    {
        var keywords = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return keywords.All(kw => IsFullKeywordMatch(title, kw));
    }

    /// <summary>
    /// 完整关键词匹配：关键词在标题中必须作为完整词/完整段出现，不是子串包含
    /// 英文：按词边界匹配（\b），中文：直接包含即可（中文无词边界概念）
    /// </summary>
    public static bool IsFullKeywordMatch(string title, string keyword)
    {
        if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(title))
            return false;

        // 中文或含非ASCII字符：直接包含匹配（中文无词边界概念）
        if (keyword.Any(c => c > 0x7F))
        {
            return title.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        // 纯英文/数字关键词：按词边界匹配
        var escaped = System.Text.RegularExpressions.Regex.Escape(keyword);
        var regex = new System.Text.RegularExpressions.Regex(
            @"\b" + escaped + @"\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return regex.IsMatch(title);
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
