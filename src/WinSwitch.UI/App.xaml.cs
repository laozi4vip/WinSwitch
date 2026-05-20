using System.Windows;
using WinSwitch.Core.Services;
using WinSwitch.UI.Views;

namespace WinSwitch.UI;

public partial class App : Application
{
    public static ConfigService ConfigService { get; private set; } = new();
    public static WindowEnumerator WindowEnumerator { get; private set; } = new();
    public static WindowSwitcher WindowSwitcher { get; private set; } = null!;
    public static BossKeyService BossKeyService { get; private set; } = null!;
    public static HotkeyService HotkeyService { get; private set; } = new();
    public static AutoStartService AutoStartService { get; private set; } = new();

    internal TrayIconManager? _trayIconManager;

    public TrayIconManager? TrayIconMgr => _trayIconManager;

    public App()
    {
        // 全局异常捕获 — 防止闪退
        DispatcherUnhandledException += (sender, e) =>
        {
            MessageBox.Show($"发生未处理的异常：\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "WinSwitch 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"发生致命异常：\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "WinSwitch 致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 初始化服务
            WindowSwitcher = new WindowSwitcher(WindowEnumerator);
            BossKeyService = new BossKeyService(WindowEnumerator, ConfigService);

            // 加载配置
            ConfigService.Load();

            // 设置日志级别
            LogService.Instance.CurrentLevel = ConfigService.Config.LogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Debug : LogLevel.Info;

            // 初始化托盘图标
            _trayIconManager = new TrayIconManager(ConfigService, HotkeyService, AutoStartService);
            _trayIconManager.Initialize();

            // 创建主窗口
            var mainWindow = new Views.MainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();

            // 注册快捷键（需要窗口句柄）
            HotkeyService.SetWindowHandle(
                new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
            HotkeyService.RegisterAll(ConfigService.Config);

            // 订阅事件
            HotkeyService.HotkeyPressed += OnHotkeyPressed;
            HotkeyService.BossKeyPressed += OnBossKeyPressed;
            WindowSwitcher.SwitchCompleted += OnSwitchCompleted;

            LogService.Instance.Info("WinSwitch 启动成功");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败：\n\n{ex.Message}\n\n{ex.StackTrace}",
                "WinSwitch 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnHotkeyPressed(string ruleId)
    {
        // 异步执行切换，避免 WndProc 中同步枚举窗口导致 UI 卡死
        Task.Run(() =>
        {
            var rule = ConfigService.Config.Rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                WindowSwitcher.Switch(rule);
            }
        });
    }

    private void OnBossKeyPressed()
    {
        // 异步执行老板键切换
        Task.Run(() =>
        {
            BossKeyService.Toggle();
        });
    }

    private void OnSwitchCompleted(SwitchResult result)
    {
        // 确保在 UI 线程执行
        Current.Dispatcher.Invoke(() =>
        {
            if (!result.Success)
            {
                TrayIconManager.ShowBalloonTip("WinSwitch", result.Message);
            }
            LogService.Instance.Info($"切换结果: {result.Message}");
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        HotkeyService.UnregisterAll();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
