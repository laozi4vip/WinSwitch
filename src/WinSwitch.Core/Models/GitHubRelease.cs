using Newtonsoft.Json;

namespace WinSwitch.Core.Models;

/// <summary>
/// GitHub Release API 响应模型（最小化）
/// </summary>
public class GitHubRelease
{
    [JsonProperty("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonProperty("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}
