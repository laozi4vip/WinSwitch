using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using WinSwitch.Core.Models;
using WinSwitch.Core.Services;

namespace WinSwitch.UI.Views;

/// <summary>
/// 系统托盘图标管理器
/// 单击打开主窗口，右键显示菜单（D2决策）
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly ConfigService _configService;
    private readonly HotkeyService _hotkeyService;
    private readonly AutoStartService _autoStartService;

    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _isPaused;

    // 保存菜单项引用以便更新状态
    private System.Windows.Forms.ToolStripMenuItem? _pauseItem;
    private System.Windows.Forms.ToolStripMenuItem? _autoStartItem;
    private System.Windows.Forms.ToolStripMenuItem? _balloonTipItem;
    private System.Windows.Forms.ToolStripMenuItem? _silentLaunchItem;

    public TrayIconManager(ConfigService configService, HotkeyService hotkeyService, AutoStartService autoStartService)
    {
        _configService = configService;
        _hotkeyService = hotkeyService;
        _autoStartService = autoStartService;
    }

    public void Initialize()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "WinSwitch — 窗口快捷切换助手",
            Visible = true
        };

        // 加载图标
        try
        {
            var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/sun.ico"));
            if (iconStream != null)
                _notifyIcon.Icon = new System.Drawing.Icon(iconStream.Stream);
            else
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        catch
        {
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        // 单击打开主窗口（D2决策）
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ShowMainWindow();
            }
        };

        // 右键菜单
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        contextMenu.Items.Add("打开主窗口", null, (_, _) => ShowMainWindow());

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _pauseItem = new System.Windows.Forms.ToolStripMenuItem("暂停快捷键");
        _pauseItem.Click += (_, _) => TogglePause();
        contextMenu.Items.Add(_pauseItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        _autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启动");
        _autoStartItem.Click += (_, _) => ToggleAutoStart();
        _autoStartItem.Checked = _autoStartService.IsEnabled;
        contextMenu.Items.Add(_autoStartItem);

        _balloonTipItem = new System.Windows.Forms.ToolStripMenuItem("气泡通知");
        _balloonTipItem.Click += (_, _) => ToggleBalloonTip();
        _balloonTipItem.Checked = _configService.Config.BalloonTipEnabled;
        contextMenu.Items.Add(_balloonTipItem);

        _silentLaunchItem = new System.Windows.Forms.ToolStripMenuItem("静默启动");
        _silentLaunchItem.Click += (_, _) => ToggleSilentLaunch();
        _silentLaunchItem.Checked = _configService.Config.SilentLaunch;
        contextMenu.Items.Add(_silentLaunchItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // 日志级别子菜单
        var logMenu = new System.Windows.Forms.ToolStripMenuItem("日志级别");
        var debugItem = new System.Windows.Forms.ToolStripMenuItem("Debug");
        debugItem.Click += (_, _) =>
        {
            LogService.Instance.CurrentLevel = LogLevel.Debug;
            _configService.Config.LogLevel = "Debug";
            _configService.Save();
        };
        var infoItem = new System.Windows.Forms.ToolStripMenuItem("Info");
        infoItem.Click += (_, _) =>
        {
            LogService.Instance.CurrentLevel = LogLevel.Info;
            _configService.Config.LogLevel = "Info";
            _configService.Save();
        };
        infoItem.Checked = true;
        logMenu.DropDownItems.AddRange(new[] { debugItem, infoItem });
        contextMenu.Items.Add(logMenu);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        contextMenu.Items.Add("退出", null, (_, _) => Application.Current.Shutdown());
            contextMenu.Items.Add("检查更新", null, (_, _) => CheckForUpdate());
            contextMenu.Items.Add("关于 WinSwitch", null, (_, _) => ShowAbout());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowMainWindow()
    {
        if (Application.Current.MainWindow is { } window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Show();
            window.Activate();
        }
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            _hotkeyService.UnregisterAll();
            ShowBalloonTip("WinSwitch", "快捷键已暂停");
        }
        else
        {
            _hotkeyService.RegisterAll(_configService.Config);
            ShowBalloonTip("WinSwitch", "快捷键已恢复");
        }

        if (_pauseItem != null)
        {
            _pauseItem.Text = _isPaused ? "恢复快捷键" : "暂停快捷键";
        }
    }

    private void ToggleSilentLaunch()
    {
        _configService.Config.SilentLaunch = !_configService.Config.SilentLaunch;
        _configService.Save();
        if (_silentLaunchItem != null)
        {
            _silentLaunchItem.Checked = _configService.Config.SilentLaunch;
        }
    }

    private void ToggleBalloonTip()
    {
        _configService.Config.BalloonTipEnabled = !_configService.Config.BalloonTipEnabled;
        _configService.Save();
        if (_balloonTipItem != null)
        {
            _balloonTipItem.Checked = _configService.Config.BalloonTipEnabled;
        }
    }

    private void ToggleAutoStart()
    {
        if (_autoStartService.IsEnabled)
        {
            _autoStartService.Disable();
        }
        else
        {
            _autoStartService.Enable();
        }

        if (_autoStartItem != null)
        {
            _autoStartItem.Checked = _autoStartService.IsEnabled;
        }
    }

    /// <summary>
    /// 显示托盘气泡通知
    /// </summary>
    public static void ShowBalloonTip(string title, string text, int timeout = 3000)
    {
        // 检查气泡通知开关
        if (Application.Current is App app)
        {
            if (!App.ConfigService.Config.BalloonTipEnabled) return;
            if (app._trayIconManager?._notifyIcon is { } icon)
            {
                icon.ShowBalloonTip(timeout, title, text, System.Windows.Forms.ToolTipIcon.Info);
            }
        }
    }

    private async void CheckForUpdate()
    {
        try
        {
            var currentVersion = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "WinSwitch");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync("https://api.github.com/repos/laozi4vip/WinSwitch/releases/latest");
            var release = Newtonsoft.Json.JsonConvert.DeserializeObject<GitHubRelease>(json);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                MessageBox.Show("检查更新失败：无法获取版本信息", "检查更新", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var latestVersion = release.TagName.TrimStart('v');
            if (IsNewerVersion(latestVersion, currentVersion))
            {
                var result = MessageBox.Show(
                    $"发现新版本 v{latestVersion}\n当前版本 v{currentVersion}\n\n是否前往下载？",
                    "检查更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true });
                }
            }
            else
            {
                MessageBox.Show($"当前已是最新版本 v{currentVersion}", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Info($"检查更新失败: {ex.Message}");
            MessageBox.Show($"检查更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowAbout()
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "未知";
        var msg = "WinSwitch v" + version + "\n窗口快捷切换助手\n\n作者: laozi4vip\n主页: https://github.com/laozi4vip/WinSwitch";
        MessageBox.Show(msg, "关于 WinSwitch", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var lv = latest.Split('.');
            var cv = current.Split('.');
            var maxLen = Math.Max(lv.Length, cv.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int l = i < lv.Length && int.TryParse(lv[i], out var ln) ? ln : 0;
                int c = i < cv.Length && int.TryParse(cv[i], out var cn) ? cn : 0;
                if (l > c) return true;
                if (l < c) return false;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
