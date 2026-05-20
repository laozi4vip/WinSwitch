using WinSwitch.Core.Models;
using WinSwitch.Core.Services;
using Xunit;

namespace WinSwitch.Tests;

public class ConfigServiceTests
{
    private readonly string _testConfigDir;
    private readonly ConfigService _configService;

    public ConfigServiceTests()
    {
        _testConfigDir = Path.Combine(Path.GetTempPath(), "WinSwitch_Test_" + Guid.NewGuid());
        Directory.CreateDirectory(_testConfigDir);
        _configService = new ConfigService();
    }

    [Fact]
    public void DefaultConfig_ShouldHaveCorrectDefaults()
    {
        var config = new AppConfig();

        Assert.Equal("1.0", config.Version);
        Assert.Equal("Ctrl+`", config.BossKey);
        Assert.Equal(BossKeyMode.HideWindowAndTaskbarAndAltTab, config.BossKeyMode);
        Assert.Empty(config.Rules);
        Assert.False(config.AutoStart);
        Assert.Equal("Info", config.LogLevel);
    }

    [Fact]
    public void AddRule_ShouldAddToRulesList()
    {
        var config = new AppConfig();
        var service = new ConfigService();

        var rule = new WindowRule
        {
            Name = "测试规则",
            Hotkey = "Ctrl+Alt+T",
            MatchMode = MatchMode.Fixed,
            ProcessName = "WeChat"
        };

        service.AddRule(rule);

        Assert.Single(service.Config.Rules);
        Assert.Equal("测试规则", service.Config.Rules[0].Name);
        Assert.NotEqual(Guid.Empty.ToString(), service.Config.Rules[0].Id);
    }

    [Fact]
    public void RemoveRule_ShouldRemoveFromList()
    {
        var service = new ConfigService();
        var rule = new WindowRule { Name = "要删除的", Hotkey = "Ctrl+Alt+D", ProcessName = "Test" };
        service.AddRule(rule);

        var removed = service.RemoveRule(rule.Id);

        Assert.True(removed);
        Assert.Empty(service.Config.Rules);
    }

    [Fact]
    public void RemoveRule_WithInvalidId_ShouldReturnFalse()
    {
        var service = new ConfigService();

        var removed = service.RemoveRule("nonexistent-id");

        Assert.False(removed);
    }

    [Fact]
    public void IsHotkeyConflict_WithBossKey_ShouldReturnTrue()
    {
        var service = new ConfigService();

        var conflict = service.IsHotkeyConflict("Ctrl+`");

        Assert.True(conflict);
    }

    [Fact]
    public void IsHotkeyConflict_WithExistingRule_ShouldReturnTrue()
    {
        var service = new ConfigService();
        service.AddRule(new WindowRule { Name = "测试", Hotkey = "Ctrl+Alt+W", ProcessName = "WeChat" });

        var conflict = service.IsHotkeyConflict("Ctrl+Alt+W");

        Assert.True(conflict);
    }

    [Fact]
    public void IsHotkeyConflict_WithSameRuleExcluded_ShouldReturnFalse()
    {
        var service = new ConfigService();
        var rule = new WindowRule { Name = "测试", Hotkey = "Ctrl+Alt+W", ProcessName = "WeChat" };
        service.AddRule(rule);

        var conflict = service.IsHotkeyConflict("Ctrl+Alt+W", rule.Id);

        Assert.False(conflict);
    }
}
