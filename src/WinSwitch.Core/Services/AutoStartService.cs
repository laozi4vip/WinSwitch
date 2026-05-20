using Microsoft.Win32;

namespace WinSwitch.Core.Services;

/// <summary>
/// 开机自启动服务
/// 通过注册表 Run 键实现
/// </summary>
public class AutoStartService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WinSwitch";

    /// <summary>
    /// 获取当前开机自启状态
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
    }

    /// <summary>
    /// 启用开机自启
    /// </summary>
    public bool Enable(string? exePath = null)
    {
        try
        {
            var path = exePath ?? Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
                return false;

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(AppName, $"\"{path}\"");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 禁用开机自启
    /// </summary>
    public bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
