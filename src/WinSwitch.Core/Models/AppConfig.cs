using Newtonsoft.Json;

namespace WinSwitch.Core.Models;

/// <summary>
/// 窗口规则配置根对象
/// </summary>
public class AppConfig
{
    [JsonProperty("version")]
    public string Version { get; set; } = "1.0";

    [JsonProperty("bossKey")]
    public string BossKey { get; set; } = "Ctrl+`";

    [JsonProperty("bossKeyMode")]
    public BossKeyMode BossKeyMode { get; set; } = BossKeyMode.HideWindowAndTaskbarAndAltTab;

    [JsonProperty("rules")]
    public List<WindowRule> Rules { get; set; } = new();

    [JsonProperty("autoStart")]
    public bool AutoStart { get; set; } = false;

    [JsonProperty("balloonTipEnabled")]
    public bool BalloonTipEnabled { get; set; } = true;

    [JsonProperty("silentLaunch")]
    public bool SilentLaunch { get; set; } = false;

    [JsonProperty("logLevel")]
    public string LogLevel { get; set; } = "Info";
}

/// <summary>
/// 窗口规则
/// </summary>
public class WindowRule
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("hotkey")]
    public string Hotkey { get; set; } = string.Empty;

    [JsonProperty("matchMode")]
    public MatchMode MatchMode { get; set; } = MatchMode.Fixed;

    [JsonProperty("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonProperty("titlePattern")]
    public string TitlePattern { get; set; } = string.Empty;

    [JsonProperty("titleMatchType")]
    public TitleMatchType TitleMatchType { get; set; } = TitleMatchType.Contains;

    [JsonProperty("bossKeyEnabled")]
    public bool BossKeyEnabled { get; set; } = true;

    [JsonProperty("hideTaskbarOnBossKey")]
    public bool HideTaskbarOnBossKey { get; set; } = true;

    [JsonProperty("hideAltTabOnBossKey")]
    public bool HideAltTabOnBossKey { get; set; } = true;

    /// <summary>
    /// Fixed 模式下缓存的窗口句柄
    /// </summary>
    [JsonIgnore]
    public IntPtr CachedHandle { get; set; }

    /// <summary>
    /// 老板键隐藏时缓存的扩展样式
    /// </summary>
    [JsonIgnore]
    public int CachedExStyle { get; set; }

    /// <summary>
    /// 是否正在被老板键隐藏
    /// </summary>
    [JsonIgnore]
    public bool IsBossKeyHidden { get; set; }

    // ===== V2 浏览器扩展匹配 =====

    /// <summary>
    /// 浏览器匹配模式（V2新增）
    /// </summary>
    [JsonProperty("browserMatchMode")]
    public BrowserMatchMode BrowserMatchMode { get; set; } = BrowserMatchMode.ActiveTabTitle;

    /// <summary>
    /// URL 匹配模式（V2新增）
    /// </summary>
    [JsonProperty("urlPattern")]
    public string UrlPattern { get; set; } = string.Empty;

    /// <summary>
    /// URL 匹配方式（V2新增）
    /// </summary>
    [JsonProperty("urlMatchType")]
    public UrlMatchType UrlMatchType { get; set; } = UrlMatchType.Contains;
}

/// <summary>
/// 匹配模式
/// </summary>
public enum MatchMode
{
    /// <summary>固定窗口句柄绑定</summary>
    Fixed,

    /// <summary>标题规则匹配</summary>
    Rule,
    /// <summary>程序名匹配（按进程名匹配窗口，无需标题规则）</summary>
    ProcessName
}

/// <summary>
/// 标题匹配方式
/// </summary>
public enum TitleMatchType
{
    Contains,
    Regex,
    StartsWith,
    Exact
}

/// <summary>
/// 老板键模式
/// </summary>
public enum BossKeyMode
{
    /// <summary>仅隐藏窗口</summary>
    HideWindowOnly = 1,

    /// <summary>隐藏窗口+任务栏图标</summary>
    HideWindowAndTaskbar = 2,

    /// <summary>隐藏窗口+任务栏+Alt+Tab</summary>
    HideWindowAndTaskbarAndAltTab = 3
}

/// <summary>
/// URL匹配方式（V2新增）
/// </summary>
public enum UrlMatchType
{
    Contains,
    StartsWith,
    Exact,
    Regex
}
