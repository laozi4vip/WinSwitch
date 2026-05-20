using System.Windows;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

public partial class RuleEditDialog : Window
{
    public WindowRule Rule { get; private set; }

    public RuleEditDialog(WindowRule? existingRule = null)
    {
        InitializeComponent();

        Rule = existingRule != null
            ? new WindowRule
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
                CachedHandle = existingRule.CachedHandle
            }
            : new WindowRule();

        // 初始化匹配模式下拉
        CmbMatchMode.ItemsSource = Enum.GetValues(typeof(MatchMode));
        CmbMatchMode.SelectedItem = Rule.MatchMode;

        // 初始化标题匹配方式下拉
        CmbTitleMatchType.ItemsSource = Enum.GetValues(typeof(TitleMatchType));
        CmbTitleMatchType.SelectedItem = Rule.TitleMatchType;

        // 填充已有值
        TxtName.Text = Rule.Name;
        TxtHotkey.Text = Rule.Hotkey;
        TxtProcessName.Text = Rule.ProcessName;
        TxtTitlePattern.Text = Rule.TitlePattern;
        ChkBossKeyEnabled.IsChecked = Rule.BossKeyEnabled;
        ChkHideTaskbar.IsChecked = Rule.HideTaskbarOnBossKey;
        ChkHideAltTab.IsChecked = Rule.HideAltTabOnBossKey;

        UpdateTitleFieldsVisibility();
    }

    private void CmbMatchMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        Rule.MatchMode = (MatchMode)CmbMatchMode.SelectedItem;
        UpdateTitleFieldsVisibility();
    }

    private void UpdateTitleFieldsVisibility()
    {
        var isRuleMode = Rule.MatchMode == MatchMode.Rule;
        LblTitlePattern.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
        TxtTitlePattern.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
        LblTitleMatchType.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
        CmbTitleMatchType.Visibility = isRuleMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.SelectAll();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        // 验证必填字段
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("请输入规则名称", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtHotkey.Text))
        {
            MessageBox.Show("请输入快捷键", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtProcessName.Text))
        {
            MessageBox.Show("请输入进程名", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 快捷键冲突检测（P2）
        if (App.ConfigService.IsHotkeyConflict(TxtHotkey.Text.Trim(), Rule.Id))
        {
            MessageBox.Show($"快捷键 \"{TxtHotkey.Text.Trim()}\" 已被占用，请更换",
                "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Rule 模式必须填写标题规则
        if (Rule.MatchMode == MatchMode.Rule && string.IsNullOrWhiteSpace(TxtTitlePattern.Text))
        {
            MessageBox.Show("规则模式下必须填写标题规则", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 保存到 Rule 对象
        Rule.Name = TxtName.Text.Trim();
        Rule.Hotkey = TxtHotkey.Text.Trim();
        Rule.ProcessName = TxtProcessName.Text.Trim();
        Rule.TitlePattern = TxtTitlePattern.Text.Trim();
        Rule.TitleMatchType = (TitleMatchType)CmbTitleMatchType.SelectedItem;
        Rule.BossKeyEnabled = ChkBossKeyEnabled.IsChecked == true;
        Rule.HideTaskbarOnBossKey = ChkHideTaskbar.IsChecked == true;
        Rule.HideAltTabOnBossKey = ChkHideAltTab.IsChecked == true;

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
