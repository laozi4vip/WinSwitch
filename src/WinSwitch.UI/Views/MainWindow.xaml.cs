using System.Windows;
using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

public partial class MainWindow : Window
{
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
        // 关闭窗口时隐藏到托盘而不是退出
        e.Cancel = true;
        this.Hide();
        TrayIconManager.ShowBalloonTip("WinSwitch", "程序已最小化到托盘");
    }

    private void LoadRules()
    {
        var rules = App.ConfigService.Config.Rules;
        RulesDataGrid.ItemsSource = rules;
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
        var dialog = new RuleEditDialog();
        if (dialog.ShowDialog() == true)
        {
            App.ConfigService.AddRule(dialog.Rule);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
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

        var dialog = new RuleEditDialog(selectedRule);
        if (dialog.ShowDialog() == true)
        {
            App.ConfigService.UpdateRule(dialog.Rule);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
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

        var result = MessageBox.Show($"确定删除规则 \"{selectedRule.Name}\"？",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            App.ConfigService.RemoveRule(selectedRule.Id);
            App.HotkeyService.RegisterAll(App.ConfigService.Config);
            LogService.Instance.Info($"删除规则: {selectedRule.Name}");
        }
    }

    private void BtnPickWindow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WindowPickerDialog();
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

            var editDialog = new RuleEditDialog(rule);
            if (editDialog.ShowDialog() == true)
            {
                App.ConfigService.AddRule(editDialog.Rule);
                App.HotkeyService.RegisterAll(App.ConfigService.Config);
            }
        }
    }

    private void BtnBossKeySettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BossKeySettingsDialog(App.ConfigService.Config);
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

        // 获取当前窗口的 HwndSource 并添加 WM_HOTKEY 消息钩子
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
