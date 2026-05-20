using System.Windows;
using WinSwitch.Core.Interop;
using WinSwitch.Core.Models;
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

    /// <summary>
    /// 浏览器桥接服务（V2新增）
    /// </summary>
    public static BrowserBridgeService BrowserBridge { get; private set; } = new();

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
                ? LogLevel.Debug
                : LogLevel.Info;

            // 启动浏览器桥接服务（V2）
            BrowserBridge.Start();
            BrowserBridge.BrowserDataUpdated += OnBrowserDataUpdated;

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

            LogService.Instance.Info("WinSwitch v2.0 启动成功（含浏览器扩展支持）");
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
                // V2: 有浏览器扩展缓存数据时，全部走扩展路径
                if (BrowserBridge.HasData)
                {
                    SwitchWithBrowserData(rule);
                }
                else
                {
                    WindowSwitcher.Switch(rule);
                }
            }
        });
    }

    /// <summary>
    /// 使用浏览器扩展数据进行窗口切换（V2优化版）
    /// 快捷键只查本地缓存，不等待扩展实时返回，确保响应无延迟
    /// </summary>
    private void SwitchWithBrowserData(WindowRule rule)
    {
        // 缓存过期提示（但不阻塞操作）
        if (!BrowserBridge.IsCacheUsable)
        {
            LogService.Instance.Info("浏览器缓存数据已过期，结果可能不准确");
        }

        var matchedWindows = BrowserBridge.FindMatchingBrowserWindows(rule);
        if (matchedWindows.Count == 0)
        {
            // 回退到 Win32 模式
            WindowSwitcher.Switch(rule);
            return;
        }

        // 将浏览器窗口与 Win32 HWND 关联（每次都刷新位置映射）
        var win32Windows = WindowEnumerator.EnumerateAllWindows();
        // 重置已关联的 HWND，重新匹配
        foreach (var bw in BrowserBridge.BrowserWindows)
            bw.MatchedHwnd = IntPtr.Zero;
        BrowserBridge.MatchBrowserWindowsToHwnd(win32Windows);

        // 级1: 用已匹配的 HWND 操作窗口
        foreach (var bw in matchedWindows)
        {
            if (bw.MatchedHwnd != IntPtr.Zero)
            {
                LogService.Instance.Info($"扩展模式操作窗口(级1-HWND): HWND={bw.MatchedHwnd}");
                if (WindowEnumerator.IsForegroundWindow(bw.MatchedHwnd))
                {
                    NativeMethods.ShowWindow(bw.MatchedHwnd, NativeMethods.SW_MINIMIZE);
                }
                else
                {
                    NativeMethods.ShowWindow(bw.MatchedHwnd, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(bw.MatchedHwnd);
                }
                return;
            }
        }

        // 级2: 用活动标签页标题在Win32窗口列表中查找
        LogService.Instance.Info("级1-HWND映射失败，尝试用标签页标题查找");
        foreach (var bw in matchedWindows)
        {
            var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
            if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
            {
                var winByTitle = win32Windows.FirstOrDefault(w =>
                    w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase) &&
                    (w.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                     w.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
                     w.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
                     w.ProcessName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase) ||
                     w.ProcessName.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
                     w.ProcessName.Equals("firefox", StringComparison.OrdinalIgnoreCase)));
                if (winByTitle != null && winByTitle.Handle != IntPtr.Zero)
                {
                    LogService.Instance.Info($"扩展模式操作窗口(级2-标题): HWND={winByTitle.Handle}, 标题='{winByTitle.Title}'");
                    if (WindowEnumerator.IsForegroundWindow(winByTitle.Handle))
                        NativeMethods.ShowWindow(winByTitle.Handle, NativeMethods.SW_MINIMIZE);
                    else
                    {
                        NativeMethods.ShowWindow(winByTitle.Handle, NativeMethods.SW_RESTORE);
                        NativeMethods.SetForegroundWindow(winByTitle.Handle);
                    }
                    return;
                }
            }
        }

        // 级3: 用规则关键词在浏览器进程窗口中查找
        LogService.Instance.Info("级2-标题查找失败，用规则关键词在浏览器窗口中查找");
        var keywordMatch = win32Windows.FirstOrDefault(w =>
            (w.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
             w.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
             w.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
             w.ProcessName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase) ||
             w.ProcessName.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
             w.ProcessName.Equals("firefox", StringComparison.OrdinalIgnoreCase)) &&
            WindowEnumerator.IsTitleMatch(w.Title, rule.TitlePattern, rule.TitleMatchType));
        if (keywordMatch != null && keywordMatch.Handle != IntPtr.Zero)
        {
            LogService.Instance.Info($"扩展模式操作窗口(级3-关键词): HWND={keywordMatch.Handle}, 标题='{keywordMatch.Title}'");
            if (WindowEnumerator.IsForegroundWindow(keywordMatch.Handle))
                NativeMethods.ShowWindow(keywordMatch.Handle, NativeMethods.SW_MINIMIZE);
            else
            {
                NativeMethods.ShowWindow(keywordMatch.Handle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(keywordMatch.Handle);
            }
            return;
        }

        // 级4: 全部失败，回退Win32模式
        LogService.Instance.Info("所有扩展匹配策略均失败，回退Win32模式");
        WindowSwitcher.Switch(rule);
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

    /// <summary>
    /// 浏览器数据更新回调（V2新增）
    /// </summary>
    private void OnBrowserDataUpdated()
    {
        LogService.Instance.Debug($"浏览器数据已更新: {BrowserBridge.BrowserWindows.Count} 个窗口");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        HotkeyService.UnregisterAll();
        BrowserBridge.Dispose();
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
