using System.Windows;
using WinSwitch.Core.Models;

namespace WinSwitch.UI.Views;

public partial class BossKeySettingsDialog : Window
{
    public string BossKey { get; private set; }
    public BossKeyMode BossKeyMode { get; private set; }

    public BossKeySettingsDialog(AppConfig config)
    {
        InitializeComponent();

        BossKey = config.BossKey;
        BossKeyMode = config.BossKeyMode;

        TxtBossKey.Text = BossKey;

        // 设置当前模式
        CmbBossKeyMode.SelectedIndex = (int)BossKeyMode - 1;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBossKey.Text))
        {
            MessageBox.Show("请输入老板键快捷键", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BossKey = TxtBossKey.Text.Trim();
        BossKeyMode = (BossKeyMode)(CmbBossKeyMode.SelectedIndex + 1);

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
