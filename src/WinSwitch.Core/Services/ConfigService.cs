using System.Reflection;
using Newtonsoft.Json;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 配置文件管理服务
/// 配置路径: %APPDATA%\WinSwitch\config.json
/// </summary>
public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinSwitch");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private AppConfig _config = new();

    public AppConfig Config => _config;

    /// <summary>
    /// 配置变更事件
    /// </summary>
    public event Action<AppConfig>? ConfigChanged;

    /// <summary>
    /// 加载配置文件，若不存在则创建默认配置
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                _config = CreateDefaultConfig();
                Save(_config);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}，使用默认配置");
            _config = CreateDefaultConfig();
        }

        ConfigChanged?.Invoke(_config);
        return _config;
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public void Save(AppConfig? config = null)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            _config = config ?? _config;
            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
            ConfigChanged?.Invoke(_config);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 添加规则
    /// </summary>
    public void AddRule(WindowRule rule)
    {
        rule.Id = Guid.NewGuid().ToString();
        _config.Rules.Add(rule);
        Save();
    }

    /// <summary>
    /// 更新规则
    /// </summary>
    public void UpdateRule(WindowRule rule)
    {
        var index = _config.Rules.FindIndex(r => r.Id == rule.Id);
        if (index >= 0)
        {
            _config.Rules[index] = rule;
            Save();
        }
    }

    /// <summary>
    /// 删除规则
    /// </summary>
    public bool RemoveRule(string ruleId)
    {
        var removed = _config.Rules.RemoveAll(r => r.Id == ruleId);
        if (removed > 0)
        {
            Save();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 更新老板键设置
    /// </summary>
    public void UpdateBossKey(string hotkey, BossKeyMode mode)
    {
        _config.BossKey = hotkey;
        _config.BossKeyMode = mode;
        Save();
    }

    /// <summary>
    /// 检查快捷键是否已被其他规则占用
    /// </summary>
    public bool IsHotkeyConflict(string hotkey, string? excludeRuleId = null)
    {
        // 检查与老板键冲突
        if (string.Equals(hotkey, _config.BossKey, StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查与其他规则冲突
        return _config.Rules.Any(r =>
            r.Id != excludeRuleId &&
            string.Equals(r.Hotkey, hotkey, StringComparison.OrdinalIgnoreCase));
    }

    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            Version = "1.0",
            BossKey = "Ctrl+`",
            BossKeyMode = BossKeyMode.HideWindowAndTaskbarAndAltTab,
            LogLevel = "Info",
            AutoStart = false,
            Rules = new List<WindowRule>()
        };
    }
}
