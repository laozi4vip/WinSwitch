using WinSwitch.Core.Services;
using Xunit;

namespace WinSwitch.Tests;

public class HotkeyServiceTests
{
    [Theory]
    [InlineData("Ctrl+Alt+W", 0x0002 | 0x0001 | 0x4000, 0x57)]  // W = 0x57
    [InlineData("Ctrl+`", 0x0002 | 0x4000, 0xC0)]               // ` = VK_OEM_3
    [InlineData("Ctrl+Shift+F1", 0x0002 | 0x0004 | 0x4000, 0x70)] // F1 = 0x70
    [InlineData("Alt+Space", 0x0001 | 0x4000, 0x20)]             // Space = 0x20
    public void ParseHotkey_ShouldReturnCorrectModifiersAndVK(string hotkey, uint expectedModifiers, uint expectedVK)
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey(hotkey);

        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedVK, vk);
    }

    [Fact]
    public void ParseHotkey_WithSingleLetter_ShouldReturnASCII()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+A");

        Assert.Equal(0x0002 | 0x4000, modifiers);
        Assert.Equal((uint)'A', vk);
    }

    [Fact]
    public void ParseHotkey_WithNumberKey_ShouldReturnASCII()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+1");

        Assert.Equal(0x0002 | 0x4000, modifiers);
        Assert.Equal((uint)'1', vk);
    }
}
