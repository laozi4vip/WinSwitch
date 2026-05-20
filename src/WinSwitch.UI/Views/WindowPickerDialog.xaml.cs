using System.Windows;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

public partial class WindowPickerDialog : Window
{
    private List<WindowInfo> _allWindows = new();

    public WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerDialog()
    {
        InitializeComponent();
        RefreshWindows();
    }

    private void RefreshWindows()
    {
        _allWindows = App.WindowEnumerator.EnumerateAllWindows();

        // 填充进程筛选下拉
        var processNames = _allWindows
            .Select(w => w.ProcessName)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        processNames.Insert(0, "（全部）");
        CmbProcessFilter.ItemsSource = processNames;
        CmbProcessFilter.SelectedIndex = 0;

        WindowsDataGrid.ItemsSource = _allWindows;
    }

    private void CmbProcessFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbProcessFilter.SelectedItem is string selectedProcess)
        {
            if (selectedProcess == "（全部）")
            {
                WindowsDataGrid.ItemsSource = _allWindows;
            }
            else
            {
                WindowsDataGrid.ItemsSource = _allWindows
                    .Where(w => w.ProcessName == selectedProcess)
                    .ToList();
            }
        }
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
