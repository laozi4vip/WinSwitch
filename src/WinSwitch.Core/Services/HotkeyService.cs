using System.Collections.Concurrent;
using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.Core.Services;

/// <summary>
/// 全局快捷键注册服务
/// 使用 Win32 RegisterHotKey / UnregisterHotKey
/// </summary>
public class HotkeyService
{
    private readonly ConcurrentDictionary<int, string> _registeredHotkeys = new();
    private int _nextId = 1;

    /// <summary>
    /// 快捷键触发事件 — 参数为规则ID
    /// </summary>
    public event Action<string>? HotkeyPressed;

    /// <summary>
    /// 老板键触发事件
    /// </summary>
    public event Action? BossKeyPressed;

    /// <summary>
    /// 注册快捷键冲突事件
    /// </summary>
    public event Action<string, string>? HotkeyConflict;

    private IntPtr _windowHandle;

    /// <summary>
    /// 设置接收 WM_HOTKEY 消息的窗口句柄
    /// </summary>
    public void SetWindowHandle(IntPtr hWnd)
    {
        _windowHandle = hWnd;
    }

    /// <summary>
    /// 注册所有规则的快捷键
    /// </summary>
    public void RegisterAll(AppConfig config)
    {
        UnregisterAll();

        // 注册老板键
        if (!string.IsNullOrEmpty(config.BossKey))
        {
            var (modifiers, vk) = ParseHotkey(config.BossKey);
            var bossKeyId = _nextId++;
            if (NativeMethods.RegisterHotKey(_windowHandle, bossKeyId, modifiers, vk))
            {
                _registeredHotkeys[bossKeyId] = "__BOSS_KEY__";
            }
            else
            {
                HotkeyConflict?.Invoke(config.BossKey, "老板键");
            }
        }

        // 注册规则快捷键
        foreach (var rule in config.Rules)
        {
            if (string.IsNullOrEmpty(rule.Hotkey))
                continue;

            var (modifiers, vk) = ParseHotkey(rule.Hotkey);
            var id = _nextId++;
            if (NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, vk))
            {
                _registeredHotkeys[id] = rule.Id;
            }
            else
            {
                HotkeyConflict?.Invoke(rule.Hotkey, rule.Name);
            }
        }
    }

    /// <summary>
    /// 注销所有快捷键
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var kvp in _registeredHotkeys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, kvp.Key);
        }
        _registeredHotkeys.Clear();
        _nextId = 1;
    }

    /// <summary>
    /// 处理 WM_HOTKEY 消息
    /// </summary>
    public void ProcessHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var ruleId))
        {
            if (ruleId == "__BOSS_KEY__")
            {
                BossKeyPressed?.Invoke();
            }
            else
            {
                HotkeyPressed?.Invoke(ruleId);
            }
        }
    }

    /// <summary>
    /// 解析快捷键字符串为修饰键+虚拟键码
    /// 格式: "Ctrl+Alt+W", "Ctrl+`"
    /// </summary>
    public static (uint modifiers, uint vk) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        uint vk = 0;

        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "WIN":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    vk = KeyToVirtualKey(part);
                    break;
            }
        }

        // 添加 MOD_NOREPEAT 防止重复触发
        modifiers |= NativeMethods.MOD_NOREPEAT;

        return (modifiers, vk);
    }

    /// <summary>
    /// 将键名转换为虚拟键码
    /// </summary>
    private static uint KeyToVirtualKey(string key)
    {
        // 单字符键
        if (key.Length == 1)
        {
            var ch = key.ToUpperInvariant()[0];
            if (ch >= 'A' && ch <= 'Z')
                return ch; // A-Z 的 VK 码就是 ASCII
            if (ch >= '0' && ch <= '9')
                return ch; // 0-9 的 VK 码就是 ASCII

            // 特殊字符映射
            return ch switch
            {
                '`' => 0xC0, // VK_OEM_3
                '-' => 0xBD, // VK_OEM_MINUS
                '=' => 0xBB, // VK_OEM_PLUS
                '[' => 0xDB, // VK_OEM_4
                ']' => 0xDD, // VK_OEM_6
                '\\' => 0xDC, // VK_OEM_5
                ';' => 0xBA, // VK_OEM_1
                '\'' => 0xDE, // VK_OEM_7
                ',' => 0xBC, // VK_OEM_COMMA
                '.' => 0xBE, // VK_OEM_PERIOD
                '/' => 0xBF, // VK_OEM_2
                _ => ch
            };
        }

        // 功能键
        return key.ToUpperInvariant() switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ENTER" => 0x0D,
            "ESCAPE" or "ESC" => 0x1B,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "NUMLOCK" => 0x90,
            "SCROLL" => 0x91,
            "CAPSLOCK" => 0x14,
            _ => 0x00
        };
    }
}
