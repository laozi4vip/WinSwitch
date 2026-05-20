namespace WinSwitch.Core.Models;

/// <summary>
/// 浏览器窗口信息（来自浏览器扩展）
/// </summary>
public class BrowserWindowInfo
{
    /// <summary>
    /// 浏览器内部窗口ID
    /// </summary>
    public int BrowserWindowId { get; set; }

    /// <summary>
    /// 是否为焦点窗口
    /// </summary>
    public bool Focused { get; set; }

    /// <summary>
    /// 窗口状态：normal/minimized/maximized/fullscreen
    /// </summary>
    public string State { get; set; } = "normal";

    /// <summary>
    /// 窗口位置X
    /// </summary>
    public int Left { get; set; }

    /// <summary>
    /// 窗口位置Y
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 窗口高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 该窗口的所有标签页
    /// </summary>
    public List<BrowserTabInfo> Tabs { get; set; } = new();

    /// <summary>
    /// 关联的 Win32 窗口句柄（由主程序匹配后填充）
    /// </summary>
    public IntPtr MatchedHwnd { get; set; }
}

/// <summary>
/// 浏览器标签页信息
/// </summary>
public class BrowserTabInfo
{
    public int TabId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string FavIconUrl { get; set; } = string.Empty;
}

/// <summary>
/// 匹配模式扩展 — V2 新增 URL 匹配和任意标签页匹配
/// </summary>
public enum BrowserMatchMode
{
    /// <summary>
    /// 仅匹配当前活动标签页标题（V1 行为）
    /// </summary>
    ActiveTabTitle,

    /// <summary>
    /// 匹配任意标签页标题（V2 新增）
    /// </summary>
    AnyTabTitle,

    /// <summary>
    /// 匹配任意标签页 URL（V2 新增）
    /// </summary>
    AnyTabUrl
}
