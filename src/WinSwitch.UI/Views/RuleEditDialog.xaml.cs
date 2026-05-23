using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using WinSwitch.Core.Models;

namespace WinSwitch.UI.Views;

public partial class RuleEditDialog : Window
{
    public WindowRule Rule { get; private set; }

    // 快捷键捕获状态
    private bool _isCapturingHotkey;
    private uint _capturedModifiers;
    private uint _capturedVk;

    // 枚举中文映射
    private static readonly Dictionary<MatchMode, string> MatchModeNames = new()
    {
        { MatchMode.Fixed, "固定窗口" },
        { MatchMode.Rule, "规则匹配" },
        { MatchMode.ProcessName, "程序名匹配" },
        { MatchMode.TaskbarPin, "任务栏固定" }
    };

    private static readonly Dictionary<TitleMatchType, string> TitleMatchTypeNames = new()
    {
        { TitleMatchType.Contains, "包含" },
        { TitleMatchType.StartsWith, "开头匹配" },
        { TitleMatchType.Exact, "精确匹配" },
        { TitleMatchType.Regex, "正则表达式" }
    };

    // V2: 浏览器匹配模式中文映射
    private static readonly Dictionary<BrowserMatchMode, string> BrowserMatchModeNames = new()
    {
        { BrowserMatchMode.ActiveTabTitle, "当前标签页标题（无需扩展）" },
        { BrowserMatchMode.AnyTabTitle, "任意标签页标题（需扩展）" },
        { BrowserMatchMode.AnyTabUrl, "任意标签页URL（需扩展）" }
    };

    // V2: URL匹配方式中文映射
    private static readonly Dictionary<UrlMatchType, string> UrlMatchTypeNames = new()
    {
        { UrlMatchType.Contains, "URL包含" },
        { UrlMatchType.StartsWith, "URL开头匹配" },
        { UrlMatchType.Exact, "URL精确匹配" },
        { UrlMatchType.Regex, "URL正则表达式" }
    };

    public RuleEditDialog(WindowRule? existingRule = null)
    {
        InitializeComponent();

        Rule = existingRule != null ? new WindowRule
        {
            Id = existingRule.Id,
            Name = existingRule.Name,
            Hotkey = existingRule.Hotkey,
            MatchMode = existingRule.MatchMode,
            ProcessName = existingRule.ProcessName,
            TitlePattern = existingRule.TitlePattern,
            TitleMatchType = existingRule.TitleMatchType,
            BossKeyEnabled = existingRule.BossKeyEnabled,
            HideTaskbarOnBossKey = existingRule.HideTaskbarOnBossKey,
            HideAltTabOnBossKey = existingRule.HideAltTabOnBossKey,
            CachedHandle = existingRule.CachedHandle,
            // V2
            BrowserMatchMode = existingRule.BrowserMatchMode,
            UrlPattern = existingRule.UrlPattern,
            UrlMatchType = existingRule.UrlMatchType,
            TaskbarSlot = existingRule.TaskbarSlot
        } : new WindowRule();

        // 初始化匹配模式下拉（中文）
        CmbMatchMode.ItemsSource = MatchModeNames.Values.ToList();
        CmbMatchMode.SelectedIndex = (int)Rule.MatchMode;

        // 初始化标题匹配方式下拉（中文）
        CmbTitleMatchType.ItemsSource = TitleMatchTypeNames.Values.ToList();
        CmbTitleMatchType.SelectedIndex = (int)Rule.TitleMatchType;

        // V2: 初始化浏览器匹配模式下拉
        CmbBrowserMatchMode.ItemsSource = BrowserMatchModeNames.Values.ToList();
        CmbBrowserMatchMode.SelectedIndex = (int)Rule.BrowserMatchMode;

        // V2: 初始化URL匹配方式下拉
        CmbUrlMatchType.ItemsSource = UrlMatchTypeNames.Values.ToList();
        CmbUrlMatchType.SelectedIndex = (int)Rule.UrlMatchType;

        // 填充已有值
        TxtName.Text = Rule.Name;
        TxtHotkey.Text = string.IsNullOrEmpty(Rule.Hotkey) ? "点击此处设置快捷键" : Rule.Hotkey;
        TxtProcessName.Text = Rule.ProcessName;
        TxtTaskbarSlot.Text = Rule.TaskbarSlot.ToString();
        TxtTitlePattern.Text = Rule.TitlePattern;
        TxtUrlPattern.Text = Rule.UrlPattern;
        ChkBossKeyEnabled.IsChecked = Rule.BossKeyEnabled;
        ChkHideTaskbar.IsChecked = Rule.HideTaskbarOnBossKey;
        ChkHideAltTab.IsChecked = Rule.HideAltTabOnBossKey;

        UpdateTitleFieldsVisibility();
        UpdateBrowserFieldsVisibility();

        // 快捷键按键捕获
        TxtHotkey.GotFocus += TxtHotkey_GotFocus;
        TxtHotkey.LostFocus += TxtHotkey_LostFocus;
        TxtHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
    }

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        TxtHotkey.Text = "请按下快捷键组合…";
    }

    private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = false;
        if (_capturedVk != 0)
        {
            TxtHotkey.Text = BuildHotkeyString(_capturedModifiers, _capturedVk);
        }
        else if (string.IsNullOrEmpty(Rule.Hotkey))
        {
            TxtHotkey.Text = "点击此处设置快捷键";
        }
        else
        {
            TxtHotkey.Text = Rule.Hotkey;
        }
    }

    private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 忽略单独的修饰键
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            uint mods = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= 0x0002;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= 0x0001;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= 0x0004;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= 0x0008;

            string preview = "";
            if ((mods & 0x0002) != 0) preview += "Ctrl+";
            if ((mods & 0x0001) != 0) preview += "Alt+";
            if ((mods & 0x0004) != 0) preview += "Shift+";
            if ((mods & 0x0008) != 0) preview += "Win+";
            TxtHotkey.Text = preview + "…";
            _capturedModifiers = mods;
            _capturedVk = 0;
            return;
        }

        // 有修饰键 + 非修饰键 → 完成组合
        uint modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 0x0008;

        // 至少需要一个修饰键，除非是 F1-F12
        bool isFunctionKey = key >= Key.F1 && key <= Key.F12;
        if (modifiers == 0 && !isFunctionKey)
        {
            TxtHotkey.Text = "需要修饰键(Ctrl/Alt/Shift/Win)或F1-F12";
            return;
        }

        var vk = KeyToVirtualKey(key);
        if (vk == 0) return;

        _capturedModifiers = modifiers;
        _capturedVk = vk;

        var hotkeyStr = BuildHotkeyString(modifiers, vk);
        TxtHotkey.Text = hotkeyStr;

        // 实时检测快捷键冲突
        if (App.ConfigService.IsHotkeyConflict(hotkeyStr, Rule.Id))
        {
            TxtHotkey.Text = $"{hotkeyStr} ⚠️ 快捷键已被占用";
        }

        // 捕获完成，移除焦点
        TxtProcessName.Focus();
    }

    private static uint KeyToVirtualKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return (uint)('A' + (key - Key.A));
        if (key >= Key.D0 && key <= Key.D9) return (uint)('0' + (key - Key.D0));
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return (uint)(0x60 + (key - Key.NumPad0));
        if (key >= Key.F1 && key <= Key.F12) return (uint)(0x70 + (key - Key.F1));

        return key switch
        {
            Key.Space => 0x20, Key.Tab => 0x09, Key.Enter => 0x0D,
            Key.Escape => 0x1B, Key.Back => 0x08, Key.Insert => 0x2D,
            Key.Delete => 0x2E, Key.Home => 0x24, Key.End => 0x23,
            Key.PageUp => 0x21, Key.PageDown => 0x22,
            Key.Up => 0x26, Key.Down => 0x28, Key.Left => 0x25, Key.Right => 0x27,
            Key.CapsLock => 0x14,
            Key.OemTilde => 0xC0, Key.OemMinus => 0xBD, Key.OemPlus => 0xBB,
            Key.OemOpenBrackets => 0xDB, Key.OemCloseBrackets => 0xDD,
            Key.OemPipe => 0xDC, Key.OemSemicolon => 0xBA, Key.OemQuotes => 0xDE,
            Key.OemComma => 0xBC, Key.OemPeriod => 0xBE, Key.OemQuestion => 0xBF,
            _ => 0
        };
    }

    private static string BuildHotkeyString(uint modifiers, uint vk)
    {
        var parts = new List<string>();
        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((modifiers & 0x0008) != 0) parts.Add("Win");

        string keyName = vk switch
        {
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x70 and <= 0x7B => $"F{vk - 0x70 + 1}",
            0xC0 => "`", 0xBD => "-", 0xBB => "=",
            0xDB => "[", 0xDD => "]", 0xDC => @"\\",
            0xBA => ";", 0xDE => "'", 0xBC => ",",
            0xBE => ".", 0xBF => "/",
            0x20 => "Space", 0x09 => "Tab", 0x0D => "Enter",
            0x1B => "Esc", 0x08 => "Backspace",
            0x2D => "Insert", 0x2E => "Delete",
            0x24 => "Home", 0x23 => "End",
            0x21 => "PageUp", 0x22 => "PageDown",
            0x26 => "Up", 0x28 => "Down",
            0x25 => "Left", 0x27 => "Right",
            _ => $"0x{vk:X2}"
        };

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private void CmbMatchMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        Rule.MatchMode = (MatchMode)CmbMatchMode.SelectedIndex;
        UpdateTitleFieldsVisibility();
    }

    // V2: 浏览器匹配模式切换
    private void CmbBrowserMatchMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        Rule.BrowserMatchMode = (BrowserMatchMode)CmbBrowserMatchMode.SelectedIndex;
        UpdateBrowserFieldsVisibility();
    }

    private void UpdateTitleFieldsVisibility()
        {
            var isRuleMode = Rule.MatchMode == MatchMode.Rule;
            var isProcessMode = Rule.MatchMode == MatchMode.ProcessName;
            var isTaskbarPinMode = Rule.MatchMode == MatchMode.TaskbarPin;

            LblTitlePattern.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
            TxtTitlePattern.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
            LblTitleHint.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
            LblTitleMatchType.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
            CmbTitleMatchType.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;

            // 任务栏固定模式：显示序号输入
            LblTaskbarSlot.Visibility = isTaskbarPinMode ? Visibility.Visible : Visibility.Collapsed;
            TxtTaskbarSlot.Visibility = isTaskbarPinMode ? Visibility.Visible : Visibility.Collapsed;
            LblTaskbarSlotHint.Visibility = isTaskbarPinMode ? Visibility.Visible : Visibility.Collapsed;

            // 程序名/任务栏固定模式下显示提示
            if (isProcessMode)
            {
                LblProcessNameHint.Text = "程序名匹配模式：按进程名匹配窗口，无需标题规则";
                LblProcessNameHint.Visibility = Visibility.Visible;
            }
            else if (isTaskbarPinMode)
            {
                LblProcessNameHint.Text = "任务栏快捷键模式：模拟 Win+数字键 激活任务栏对应位置的应用";
                LblProcessNameHint.Visibility = Visibility.Visible;
            }
            else
            {
                LblProcessNameHint.Visibility = Visibility.Collapsed;
            }
        }

        
    private void UpdateBrowserFieldsVisibility()
    {
        var isUrlMode = Rule.BrowserMatchMode == BrowserMatchMode.AnyTabUrl;
        LblUrlPattern.Visibility = isUrlMode ? Visibility.Visible : Visibility.Collapsed;
        TxtUrlPattern.Visibility = isUrlMode ? Visibility.Visible : Visibility.Collapsed;
        LblUrlHint.Visibility = isUrlMode ? Visibility.Visible : Visibility.Collapsed;
        LblUrlMatchType.Visibility = isUrlMode ? Visibility.Visible : Visibility.Collapsed;
        CmbUrlMatchType.Visibility = isUrlMode ? Visibility.Visible : Visibility.Collapsed;

        // 根据浏览器匹配模式更新标题规则提示
        LblTitleHint.Text = Rule.BrowserMatchMode switch
        {
            BrowserMatchMode.AnyTabTitle => "所有关键词都必须匹配，用分号分隔，如：客服;订单 → 需同时匹配两者",
            BrowserMatchMode.AnyTabUrl => "窗口标题规则（URL匹配为主，标题规则可选）",
            _ => "所有关键词都必须完全匹配（用分号分隔），如：Chrome;工作台 → 需同时匹配两者"
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("请输入规则名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_capturedVk == 0 && string.IsNullOrEmpty(Rule.Hotkey))
        {
            MessageBox.Show("请设置快捷键（点击输入框，按下组合键）", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtProcessName.Text))
        {
            MessageBox.Show("请输入进程名", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Rule.MatchMode == MatchMode.Rule && string.IsNullOrWhiteSpace(TxtTitlePattern.Text))
        {
            MessageBox.Show("规则模式下必须填写标题规则", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Rule.MatchMode == MatchMode.TaskbarPin && (!int.TryParse(TxtTaskbarSlot.Text, out int slotVal) || slotVal < 1 || slotVal > 10))
        {
            MessageBox.Show("任务栏模式下必须输入有效的序号 (1-10)", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // V2: URL 匹配模式需要填写 URL 规则
        if (Rule.BrowserMatchMode == BrowserMatchMode.AnyTabUrl && string.IsNullOrWhiteSpace(TxtUrlPattern.Text))
        {
            MessageBox.Show("URL匹配模式下必须填写URL规则", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 保存到 Rule 对象
        Rule.Name = TxtName.Text.Trim();
        Rule.Hotkey = _capturedVk != 0 ? BuildHotkeyString(_capturedModifiers, _capturedVk) : Rule.Hotkey;
        Rule.ProcessName = TxtProcessName.Text.Trim();
        if (Rule.MatchMode == MatchMode.TaskbarPin && int.TryParse(TxtTaskbarSlot.Text, out int slot))
        {
            Rule.TaskbarSlot = slot;
        }
        Rule.TitlePattern = TxtTitlePattern.Text.Trim();
        Rule.TitleMatchType = (TitleMatchType)CmbTitleMatchType.SelectedIndex;
        Rule.BossKeyEnabled = ChkBossKeyEnabled.IsChecked == true;
        Rule.HideTaskbarOnBossKey = ChkHideTaskbar.IsChecked == true;
        Rule.HideAltTabOnBossKey = ChkHideAltTab.IsChecked == true;

        // V2: 保存浏览器匹配相关字段
        Rule.BrowserMatchMode = (BrowserMatchMode)CmbBrowserMatchMode.SelectedIndex;
        Rule.UrlPattern = TxtUrlPattern.Text.Trim();
        Rule.UrlMatchType = (UrlMatchType)CmbUrlMatchType.SelectedIndex;

        // 快捷键冲突检测
        if (App.ConfigService.IsHotkeyConflict(Rule.Hotkey, Rule.Id))
        {
            MessageBox.Show($"快捷键 \"{Rule.Hotkey}\" 已被占用，请更换", "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
