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

            // 静默启动：开机自启时不显示主窗口（窗口句柄仍会创建，快捷键可注册）
            if (!ConfigService.Config.SilentLaunch)
            {
                mainWindow.Show();
            }
            else
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.Show();
                mainWindow.Hide();
                LogService.Instance.Info("静默启动模式，主窗口已隐藏到托盘");
            }

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
        foreach (var bw in BrowserBridge.BrowserWindows)
            bw.MatchedHwnd = IntPtr.Zero;
        BrowserBridge.MatchBrowserWindowsToHwnd(win32Windows);

        // 核心逻辑：多窗口同站时，按"前台→最小化，非前台→激活"切换
        // 优先操作已绑定的窗口（CachedBrowserWindowId），避免轮询

        if (matchedWindows.Count == 1)
        {
            // 只有一个匹配窗口，直接操作
            var bw = matchedWindows[0];
            var hWnd = ResolveHwnd(bw, win32Windows, rule);
            if (hWnd != IntPtr.Zero)
            {
                SwitchHwnd(hWnd, rule);
                rule.CachedBrowserWindowId = bw.BrowserWindowId;
                return;
            }
        }

        // 多个匹配窗口：先找已绑定的窗口
        var boundWindow = matchedWindows.FirstOrDefault(bw => bw.BrowserWindowId == rule.CachedBrowserWindowId);

        if (boundWindow != null)
        {
            var hWnd = ResolveHwnd(boundWindow, win32Windows, rule);
            if (hWnd != IntPtr.Zero)
            {
                if (WindowEnumerator.IsForegroundWindow(hWnd))
                {
                    // 前台 → 最小化
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                    LogService.Instance.Info($"扩展模式: 已绑定窗口前台→最小化, HWND={hWnd}");
                    return;
                }
                else
                {
                    // 非前台 → 激活
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(hWnd);
                    LogService.Instance.Info($"扩展模式: 已绑定窗口非前台→激活, HWND={hWnd}");
                    return;
                }
            }
        }

        // 未绑定或绑定失效：选择第一个有 HWND 的窗口绑定
        foreach (var bw in matchedWindows)
        {
            var hWnd = ResolveHwnd(bw, win32Windows, rule);
            if (hWnd != IntPtr.Zero)
            {
                // 绑定此窗口
                rule.CachedBrowserWindowId = bw.BrowserWindowId;

                if (WindowEnumerator.IsForegroundWindow(hWnd))
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                    LogService.Instance.Info($"扩展模式: 新绑定窗口前台→最小化, BrowserWindowId={bw.BrowserWindowId}, HWND={hWnd}");
                }
                else
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(hWnd);
                    LogService.Instance.Info($"扩展模式: 新绑定窗口非前台→激活, BrowserWindowId={bw.BrowserWindowId}, HWND={hWnd}");
                }
                return;
            }
        }

        // 所有 HWND 解析都失败，回退 Win32
        LogService.Instance.Info("扩展模式: 所有HWND解析失败，回退Win32模式");
        WindowSwitcher.Switch(rule);
    }

    /// <summary>
    /// 解析浏览器窗口的 HWND
    /// 级1: 直接映射 → 级2: 标题匹配(排除已用) → 级3: 位置/尺寸匹配 → 级4: 标题匹配(兜底)
    /// </summary>
    private IntPtr ResolveHwnd(BrowserWindowInfo bw, List<WindowInfo> win32Windows, WindowRule rule)
    {
        // 级1: 直接 HWND 映射（MatchBrowserWindowsToHwnd 已关联）
        if (bw.MatchedHwnd != IntPtr.Zero && NativeMethods.IsWindow(bw.MatchedHwnd))
        {
            return bw.MatchedHwnd;
        }

        // 级2: 标题匹配 —— 优先匹配未被其他规则绑定的 HWND
        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
        if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
        {
            // 收集已被其他规则占用的 HWND（CachedBrowserWindowId）
            var takenHwnds = new HashSet<IntPtr>();
            foreach (var r in ConfigService.Config.Rules)
            {
                if (r.Id == rule.Id) continue;
                if (r.CachedBrowserWindowId != 0 && r.MatchedHwnd != IntPtr.Zero)
                {
                    takenHwnds.Add(r.MatchedHwnd);
                }
            }

            // 优先：同标题但未被占用的窗口
            var winByTitle = win32Windows.FirstOrDefault(w =>
                !takenHwnds.Contains(w.Handle)
                && w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase)
                && IsBrowserProcess(w.ProcessName));
            if (winByTitle != null && winByTitle.Handle != IntPtr.Zero)
            {
                // 将此 HWND 登记到规则的 MatchedHwnd，防止后续被别的规则误用
                rule.MatchedHwnd = winByTitle.Handle;
                return winByTitle.Handle;
            }
        }

        // 级3: 位置/尺寸匹配 —— 解决同标题窗口无法区分的问题
        var posMatched = win32Windows.FirstOrDefault(w =>
            Math.Abs(w.Left - bw.Left) <= 50 &&
            Math.Abs(w.Top - bw.Top) <= 50 &&
            Math.Abs(w.Width - bw.Width) <= 50 &&
            Math.Abs(w.Height - bw.Height) <= 50 &&
            IsBrowserProcess(w.ProcessName));
        if (posMatched != null && posMatched.Handle != IntPtr.Zero)
        {
            rule.MatchedHwnd = posMatched.Handle;
            return posMatched.Handle;
        }

        // 级4: 标题兜底匹配（忽略占用检查）
        if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
        {
            var winByTitle2 = win32Windows.FirstOrDefault(w =>
                w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase)
                && IsBrowserProcess(w.ProcessName));
            if (winByTitle2 != null && winByTitle2.Handle != IntPtr.Zero)
            {
                rule.MatchedHwnd = winByTitle2.Handle;
                return winByTitle2.Handle;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 对 HWND 执行切换（前台→最小化，非前台→激活）
    /// </summary>
    private void SwitchHwnd(IntPtr hWnd, WindowRule rule)
    {
        if (WindowEnumerator.IsForegroundWindow(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
            LogService.Instance.Info($"扩展模式: 前台→最小化, HWND={hWnd}");
        }
        else
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(hWnd);
            LogService.Instance.Info($"扩展模式: 非前台→激活, HWND={hWnd}");
        }
    }

    private static bool IsBrowserProcess(string processName)
    {
        return processName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("msedge", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("brave", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("vivaldi", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("opera", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("firefox", StringComparison.OrdinalIgnoreCase);
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
