using System.Windows;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

public partial class WindowPickerDialog : Window
{
    public WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerDialog()
    {
        InitializeComponent();
        RefreshWindows();
    }

    private void RefreshWindows()
    {
        var windows = App.WindowEnumerator.EnumerateAllWindows();
        WindowsDataGrid.ItemsSource = windows;
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsDataGrid.SelectedItem is not WindowInfo selectedWindow)
        {
            MessageBox.Show("请先选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedWindow = selectedWindow;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
