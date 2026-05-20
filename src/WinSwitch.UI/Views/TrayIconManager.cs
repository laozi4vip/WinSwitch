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
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };

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
        if (Application.Current.MainWindow is { } window)
        {
            window.Dispatcher.Invoke(() =>
            {
                if (Application.Current is App app && app._trayIconManager?._notifyIcon is { } icon)
                {
                    icon.ShowBalloonTip(timeout, title, text, System.Windows.Forms.ToolTipIcon.Info);
                }
            });
        }
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
