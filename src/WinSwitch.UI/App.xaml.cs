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
    internal TrayIconManager? TrayIconManagerField => _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化服务
        WindowSwitcher = new WindowSwitcher(WindowEnumerator);
        BossKeyService = new BossKeyService(WindowEnumerator, ConfigService);

        // 加载配置
        ConfigService.Load();

        // 设置日志级别
        LogService.Instance.CurrentLevel =
            ConfigService.Config.LogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Debug
                : LogLevel.Info;

        // 初始化托盘图标
        _trayIconManager = new TrayIconManager(ConfigService, HotkeyService, AutoStartService);
        _trayIconManager.Initialize();

        // 注册快捷键（需要窗口句柄，在 MainWindow 创建后注册）
        Current.MainWindow = new Views.MainWindow();
        Current.MainWindow.Show();

        HotkeyService.SetWindowHandle(
            new System.Windows.Interop.WindowInteropHelper(Current.MainWindow).Handle);
        HotkeyService.RegisterAll(ConfigService.Config);

        // 订阅事件
        HotkeyService.HotkeyPressed += OnHotkeyPressed;
        HotkeyService.BossKeyPressed += OnBossKeyPressed;
        WindowSwitcher.SwitchCompleted += OnSwitchCompleted;
    }

    private void OnHotkeyPressed(string ruleId)
    {
        var rule = ConfigService.Config.Rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            WindowSwitcher.Switch(rule);
        }
    }

    private void OnBossKeyPressed()
    {
        BossKeyService.Toggle();
    }

    private void OnSwitchCompleted(SwitchResult result)
    {
        if (!result.Success)
        {
            // 规则匹配失败 → 托盘气泡通知（D5决策）
            TrayIconManager.ShowBalloonTip("WinSwitch", result.Message);
        }

        LogService.Instance.Info($"切换结果: {result.Message}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        HotkeyService.UnregisterAll();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
