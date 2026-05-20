using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

/// <summary>
/// 匹配模式转中文
/// </summary>
public class MatchModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MatchMode m ? m switch { MatchMode.Fixed => "固定窗口", MatchMode.Rule => "规则匹配", _ => m.ToString() } : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 标题匹配方式转中文
/// </summary>
public class TitleMatchTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TitleMatchType t ? t switch
        {
            TitleMatchType.Contains => "包含",
            TitleMatchType.StartsWith => "开头匹配",
            TitleMatchType.Exact => "精确匹配",
            TitleMatchType.Regex => "正则表达式",
            _ => t.ToString()
        } : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public partial class MainWindow : Window
{
    private ObservableCollection<WindowRule> _rulesCollection = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadRules();
        UpdateBossKeyDisplay();
        App.ConfigService.ConfigChanged += OnConfigChanged;
        App.BossKeyService.BossKeyToggled += OnBossKeyToggled;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
        TrayIconManager.ShowBalloonTip("WinSwitch", "程序已最小化到托盘");
    }

    private void LoadRules()
    {
        _rulesCollection = new ObservableCollection<WindowRule>(App.ConfigService.Config.Rules);
        RulesDataGrid.ItemsSource = _rulesCollection;
    }

    private void UpdateBossKeyDisplay()
    {
        var config = App.ConfigService.Config;
        TxtBossKey.Text = config.BossKey;
        TxtBossKeyMode.Text = config.BossKeyMode switch
        {
            BossKeyMode.HideWindowOnly => "仅隐藏窗口",
            BossKeyMode.HideWindowAndTaskbar => "隐藏+任务栏",
            BossKeyMode.HideWindowAndTaskbarAndAltTab => "隐藏+任务栏+Alt+Tab",
            _ => "未知"
        };
    }

    private void OnConfigChanged(AppConfig config)
    {
        Dispatcher.Invoke(() =>
        {
            LoadRules();
            UpdateBossKeyDisplay();
        });
    }

    private void OnBossKeyToggled(bool isHidden)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = isHidden ? "🔴 老板键已隐藏" : "就绪";
        });
    }

    private void BtnAddRule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RuleEditDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            App.ConfigService.AddRule(dialog.Rule);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
            LoadRules();
            LogService.Instance.Info($"添加规则: {dialog.Rule.Name}");
        }
    }

    private void BtnEditRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesDataGrid.SelectedItem is not WindowRule selectedRule)
        {
            MessageBox.Show("请先选择一条规则", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new RuleEditDialog(selectedRule) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            App.ConfigService.UpdateRule(dialog.Rule);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
            LoadRules();
            LogService.Instance.Info($"更新规则: {dialog.Rule.Name}");
        }
    }

    private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesDataGrid.SelectedItem is not WindowRule selectedRule)
        {
            MessageBox.Show("请先选择一条规则", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = MessageBox.Show($"确定删除规则 \"{selectedRule.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            App.ConfigService.RemoveRule(selectedRule.Id);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
            LoadRules();
            LogService.Instance.Info($"删除规则: {selectedRule.Name}");
        }
    }

    private void BtnPickWindow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WindowPickerDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
        {
            var rule = new WindowRule
            {
                Name = dialog.SelectedWindow.Title,
                MatchMode = MatchMode.Fixed,
                ProcessName = dialog.SelectedWindow.ProcessName,
                CachedHandle = dialog.SelectedWindow.Handle,
                Hotkey = string.Empty
            };
            var editDialog = new RuleEditDialog(rule) { Owner = this };
            if (editDialog.ShowDialog() == true)
            {
                App.ConfigService.AddRule(editDialog.Rule);
                App.HotkeyService.RegisterAll(App.ConfigService.Config);
                LoadRules();
            }
        }
    }

    private void BtnBossKeySettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BossKeySettingsDialog(App.ConfigService.Config) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            App.ConfigService.UpdateBossKey(dialog.BossKey, dialog.BossKeyMode);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadRules();
        TxtStatus.Text = "已刷新";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = System.Windows.Interop.HwndSource.FromHwnd(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        helper?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            App.HotkeyService.ProcessHotkeyMessage(hotkeyId);
            handled = true;
        }
        return IntPtr.Zero;
    }
}
