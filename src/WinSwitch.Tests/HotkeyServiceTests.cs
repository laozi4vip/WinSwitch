using WinSwitch.Core.Services;
using Xunit;

namespace WinSwitch.Tests;

public class HotkeyServiceTests
{
    [Fact]
    public void ParseHotkey_CtrlAltW_ShouldReturnCorrectValues()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+Alt+W");
        Assert.Equal((uint)(0x0002 | 0x0001 | 0x4000), modifiers);
        Assert.Equal((uint)0x57, vk);
    }

    [Fact]
    public void ParseHotkey_CtrlBacktick_ShouldReturnVK_OEM_3()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+`");
        Assert.Equal((uint)(0x0002 | 0x4000), modifiers);
        Assert.Equal((uint)0xC0, vk);
    }

    [Fact]
    public void ParseHotkey_CtrlShiftF1_ShouldReturnVK_F1()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+Shift+F1");
        Assert.Equal((uint)(0x0002 | 0x0004 | 0x4000), modifiers);
        Assert.Equal((uint)0x70, vk);
    }

    [Fact]
    public void ParseHotkey_AltSpace_ShouldReturnVK_SPACE()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Alt+Space");
        Assert.Equal((uint)(0x0001 | 0x4000), modifiers);
        Assert.Equal((uint)0x20, vk);
    }

    [Fact]
    public void ParseHotkey_SingleLetterA_ShouldReturnASCII()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+A");
        Assert.Equal((uint)(0x0002 | 0x4000), modifiers);
        Assert.Equal((uint)'A', vk);
    }

    [Fact]
    public void ParseHotkey_NumberKey1_ShouldReturnASCII()
    {
        var (modifiers, vk) = HotkeyService.ParseHotkey("Ctrl+1");
        Assert.Equal((uint)(0x0002 | 0x4000), modifiers);
        Assert.Equal((uint)'1', vk);
    }
}
