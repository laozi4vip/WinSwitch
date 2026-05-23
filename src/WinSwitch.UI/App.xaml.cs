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
                // TaskbarPin 模式始终走 WindowSwitcher（模拟 Win+数字键）
                if (rule.MatchMode == MatchMode.TaskbarPin)
                {
                    WindowSwitcher.Switch(rule);
                }
                else if (BrowserBridge.HasData)
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

        // 级0: 硬绑定优先 —— 如果已绑定了 HWND 且该窗口仍然存在，直接操作，完全绕开所有匹配
        if (rule.CachedBrowserHwnd != IntPtr.Zero && NativeMethods.IsWindow(rule.CachedBrowserHwnd))
        {
            SwitchHwnd(rule.CachedBrowserHwnd, rule);
            return;
        }

        // 硬绑定失效，清除状态重新匹配
        if (rule.CachedBrowserHwnd != IntPtr.Zero)
        {
            rule.CachedBrowserHwnd = IntPtr.Zero;
            rule.CachedBrowserWindowId = 0;
        }

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

        // 收集已被其他规则硬绑定的 HWND（HWND 级互斥，同 HWND 不能绑定到两个规则）
        var takenHwnds = new HashSet<IntPtr>();
        foreach (var r in ConfigService.Config.Rules)
        {
            if (r.Id == rule.Id) continue;
            if (r.CachedBrowserHwnd != IntPtr.Zero && NativeMethods.IsWindow(r.CachedBrowserHwnd))
            {
                takenHwnds.Add(r.CachedBrowserHwnd);
            }
        }

        // 过滤掉已经被其他规则硬绑定 HWND 的浏览器窗口
        // 多个相同页面的窗口只要 HWND 不同，就可以分别绑定
        var availableWindows = new List<BrowserWindowInfo>();
        foreach (var bw in matchedWindows)
        {
            var hwnd = bw.MatchedHwnd;
            if (hwnd == IntPtr.Zero) hwnd = ResolveHwnd(bw, win32Windows, rule);
            if (hwnd != IntPtr.Zero && !takenHwnds.Contains(hwnd))
            {
                availableWindows.Add(bw);
            }
            else
            {
                bw.MatchedHwnd = IntPtr.Zero; // 重置，避免干扰后续
            }
        }

        if (availableWindows.Count == 0)
        {
            LogService.Instance.Info($"扩展模式: 所有{matchedWindows.Count}个匹配窗口的HWND已被其他规则绑定, 回退Win32");
            WindowSwitcher.Switch(rule);
            return;
        }

        // 优先操作已绑定的窗口（CachedBrowserWindowId）
        var boundWindow = availableWindows.FirstOrDefault(bw => bw.BrowserWindowId == rule.CachedBrowserWindowId);
        if (boundWindow != null)
        {
            var hWnd = ResolveHwnd(boundWindow, win32Windows, rule);
            if (hWnd != IntPtr.Zero)
            {
                rule.CachedBrowserHwnd = hWnd;
                SwitchHwnd(hWnd, rule);
                return;
            }
        }

        // 未绑定或绑定失效：选择最佳窗口绑定
        // 策略：多窗口时按位置排序（先Top后Left），确保同站窗口稳定分配
        // 规则A绑定Top最小/Left最小的窗口，规则B绑定下一个，避免随机分配
        var sortedWindows = availableWindows
            .OrderBy(bw => bw.Top)
            .ThenBy(bw => bw.Left)
            .ToList();

        // 已被其他规则绑定的 HWND 在 availableWindows 中已排除
        // 优先选择当前焦点窗口（同一位置），否则选排序后的第一个（稳定分配）
        var bestWindow = sortedWindows.FirstOrDefault(bw => bw.Focused)
                         ?? sortedWindows.FirstOrDefault();

        if (bestWindow != null)
        {
            var hWnd = ResolveHwnd(bestWindow, win32Windows, rule);
            if (hWnd != IntPtr.Zero)
            {
                rule.CachedBrowserWindowId = bestWindow.BrowserWindowId;
                rule.CachedBrowserHwnd = hWnd;
                SwitchHwnd(hWnd, rule);
                LogService.Instance.Info($"扩展模式: 绑定窗口 BrowserWindowId={bestWindow.BrowserWindowId}, Focused={bestWindow.Focused}, HWND={hWnd}");
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

        // 构建「已被其他浏览器窗口占用」的 HWND 集合（跨窗口互斥，防止同标题窗口映射漂移）
        var takenHwnds = new HashSet<IntPtr>();
        foreach (var rbw in BrowserBridge.BrowserWindows)
        {
            if (rbw.BrowserWindowId == bw.BrowserWindowId) continue;
            if (rbw.MatchedHwnd != IntPtr.Zero && NativeMethods.IsWindow(rbw.MatchedHwnd))
            {
                takenHwnds.Add(rbw.MatchedHwnd);
            }
        }

        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);

        // 级2: 标题匹配 —— 排除已被其他浏览器窗口占用的 HWND
        if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
        {
            var winByTitle = win32Windows.FirstOrDefault(w =>
                !takenHwnds.Contains(w.Handle)
                && w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase)
                && IsBrowserProcess(w.ProcessName));
            if (winByTitle != null && winByTitle.Handle != IntPtr.Zero)
            {
                rule.MatchedHwnd = winByTitle.Handle;
                return winByTitle.Handle;
            }
        }

        // 级3: 位置/尺寸匹配 —— 排除已被其他浏览器窗口占用的 HWND
        var posMatched = win32Windows.FirstOrDefault(w =>
            !takenHwnds.Contains(w.Handle)
            && Math.Abs(w.Left - bw.Left) <= 50
            && Math.Abs(w.Top - bw.Top) <= 50
            && Math.Abs(w.Width - bw.Width) <= 50
            && Math.Abs(w.Height - bw.Height) <= 50
            && IsBrowserProcess(w.ProcessName));
        if (posMatched != null && posMatched.Handle != IntPtr.Zero)
        {
            rule.MatchedHwnd = posMatched.Handle;
            return posMatched.Handle;
        }

        // 级4: 标题兜底匹配 —— 同样排除已被占用的 HWND
        if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
        {
            var winByTitle2 = win32Windows.FirstOrDefault(w =>
                !takenHwnds.Contains(w.Handle)
                && w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase)
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
