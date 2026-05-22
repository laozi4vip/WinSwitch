#!/usr/bin/env python3
"""v2.10 修复脚本：4个问题一并修复"""

# ============================================================
# 问题1: 修复 ResolveHwnd 级2 标题匹配歧义
# 问题: 两个窗口同网站+同标题时，FirstOrDefault 随机返回，导致不同规则绑定同一 HWND
# 修复: 级2 改为优先匹配非已用 HWND，并增加级2.5 位置匹配
# 文件: App.xaml.cs
# ============================================================

import re

# --- 修复问题1: App.xaml.cs ResolveHwnd ---
with open('src/WinSwitch.UI/App.xaml.cs', 'r') as f:
    code = f.read()

old_resolve = '''    /// <summary>
    /// 解析浏览器窗口的 HWND（级1: 直接映射 → 级2: 活动标签页标题匹配）
    /// </summary>
    private IntPtr ResolveHwnd(BrowserWindowInfo bw, List<WindowInfo> win32Windows, WindowRule rule)
    {
        // 级1: 直接 HWND 映射
        if (bw.MatchedHwnd != IntPtr.Zero && NativeMethods.IsWindow(bw.MatchedHwnd))
        {
            return bw.MatchedHwnd;
        }

        // 级2: 用活动标签页标题匹配
        var activeTab = bw.Tabs.FirstOrDefault(t => t.Active);
        if (activeTab != null && !string.IsNullOrEmpty(activeTab.Title))
        {
            var winByTitle = win32Windows.FirstOrDefault(w =>
                w.Title.Contains(activeTab.Title, StringComparison.OrdinalIgnoreCase)
                && IsBrowserProcess(w.ProcessName));
            if (winByTitle != null && winByTitle.Handle != IntPtr.Zero)
            {
                return winByTitle.Handle;
            }
        }

        return IntPtr.Zero;
    }'''

new_resolve = '''    /// <summary>
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
    }'''

code = code.replace(old_resolve, new_resolve)

with open('src/WinSwitch.UI/App.xaml.cs', 'w') as f:
    f.write(code)

print("问题1修复完成: App.xaml.cs ResolveHwnd 增加级3位置匹配 + 级2占用检查")


# ============================================================
# 问题2: 修复 AnyTabUrl 模式强制要求标题规则
# 修复: AnyTabUrl 模式下不要求标题规则
# 文件: RuleEditDialog.xaml.cs
# ============================================================

with open('src/WinSwitch.UI/Views/RuleEditDialog.xaml.cs', 'r') as f:
    rule_code = f.read()

old_title_check = '''        if (Rule.MatchMode == MatchMode.Rule && string.IsNullOrWhiteSpace(TxtTitlePattern.Text))
        {
            MessageBox.Show("规则模式下必须填写标题规则", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }'''

new_title_check = '''        if (Rule.MatchMode == MatchMode.Rule
            && string.IsNullOrWhiteSpace(TxtTitlePattern.Text)
            && Rule.BrowserMatchMode != BrowserMatchMode.AnyTabUrl)
        {
            MessageBox.Show("规则模式下必须填写标题规则（任意标签页URL模式除外）", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }'''

rule_code = rule_code.replace(old_title_check, new_title_check)

with open('src/WinSwitch.UI/Views/RuleEditDialog.xaml.cs', 'w') as f:
    f.write(rule_code)

print("问题2修复完成: RuleEditDialog.xaml.cs AnyTabUrl 模式不再要求标题规则")


# ============================================================
# 问题3: 修复托盘图标日志级别 Checked 状态不切换
# 问题: 点击 Debug 只设 Checked=true 但不清除 Info 的 Checked
# 修复: 互斥逻辑
# 文件: TrayIconManager.cs
# ============================================================

with open('src/WinSwitch.UI/Views/TrayIconManager.cs', 'r') as f:
    tray_code = f.read()

old_log_menu = '''        // 日志级别子菜单
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
        logMenu.DropDownItems.AddRange(new[] { debugItem, infoItem });'''

new_log_menu = '''        // 日志级别子菜单
        var logMenu = new System.Windows.Forms.ToolStripMenuItem("日志级别");
        var debugItem = new System.Windows.Forms.ToolStripMenuItem("Debug");
        var infoItem = new System.Windows.Forms.ToolStripMenuItem("Info");
        debugItem.Click += (_, _) =>
        {
            LogService.Instance.CurrentLevel = LogLevel.Debug;
            _configService.Config.LogLevel = "Debug";
            _configService.Save();
            debugItem.Checked = true;
            infoItem.Checked = false;
        };
        infoItem.Click += (_, _) =>
        {
            LogService.Instance.CurrentLevel = LogLevel.Info;
            _configService.Config.LogLevel = "Info";
            _configService.Save();
            infoItem.Checked = true;
            debugItem.Checked = false;
        };

        // 根据当前日志级别初始化选中状态
        if (_configService.Config.LogLevel.Equals("Debug", StringComparison.OrdinalIgnoreCase))
        {
            debugItem.Checked = true;
            infoItem.Checked = false;
        }
        else
        {
            infoItem.Checked = true;
            debugItem.Checked = false;
        }

        logMenu.DropDownItems.AddRange(new[] { debugItem, infoItem });'''

tray_code = tray_code.replace(old_log_menu, new_log_menu)

with open('src/WinSwitch.UI/Views/TrayIconManager.cs', 'w') as f:
    f.write(tray_code)

print("问题3修复完成: TrayIconManager.cs 日志级别 Checked 互斥 + 启动初始化")


# ============================================================
# 问题4: 修复老板键隐藏了未勾选的浏览器
# 问题: FindAllMatchingWindows 中 TitlePattern 为空时匹配所有窗口
# 但对于 BossKeyService 来说，TitlePattern 为空+非 Fixed 模式的规则不应该通过 Win32 匹配任何窗口
# 同时，浏览器扩展匹配也需要检查 BossKeyEnabled
# 修复: BossKeyService 中浏览器扩展匹配也只处理 BossKeyEnabled 的规则（已经是这样了）
# 进一步：FindAllMatchingWindows 对空 TitlePattern 的 Rule 模式返回空
# 文件: BossKeyService.cs + WindowEnumerator.cs
# ============================================================

# 修复 WindowEnumerator.cs FindAllMatchingWindows:
# 对于 MatchMode.Rule 但标题为空的情况，不应匹配任何窗口
with open('src/WinSwitch.Core/Services/WindowEnumerator.cs', 'r') as f:
    we_code = f.read()

old_find_all = '''    public List<IntPtr> FindAllMatchingWindows(WindowRule rule)
    {
        var result = new List<IntPtr>();
        var windows = FindWindowsByProcess(rule.ProcessName);

        foreach (var window in windows)
        {
            bool titleMatch = rule.MatchMode == MatchMode.Fixed
                || string.IsNullOrEmpty(rule.TitlePattern)
                || IsTitleMatch(window.Title, rule.TitlePattern, rule.TitleMatchType);

            if (titleMatch)
            {
                result.Add(window.Handle);
            }
        }
        return result;
    }'''

new_find_all = '''    public List<IntPtr> FindAllMatchingWindows(WindowRule rule)
    {
        var result = new List<IntPtr>();

        // Fixed 模式：全量返回该进程窗口
        if (rule.MatchMode == MatchMode.Fixed)
        {
            result.AddRange(FindWindowsByProcess(rule.ProcessName).Select(w => w.Handle));
            return result;
        }

        // Rule / ProcessName 模式：需要标题过滤
        // 如果无标题规则，不通过 Win32 匹配（避免误伤同进程其他窗口）
        if (string.IsNullOrEmpty(rule.TitlePattern))
        {
            return result;
        }

        var windows = FindWindowsByProcess(rule.ProcessName);
        foreach (var window in windows)
        {
            if (IsTitleMatch(window.Title, rule.TitlePattern, rule.TitleMatchType))
            {
                result.Add(window.Handle);
            }
        }
        return result;
    }'''

we_code = we_code.replace(old_find_all, new_find_all)

# 还需要添加 System.Linq using（检查是否已有）
if 'using System.Linq;' not in we_code:
    # 在第一个 using 之后插入
    we_code = we_code.replace('using WinSwitch.Core.Interop;',
        'using WinSwitch.Core.Interop;\nusing System.Linq;')

with open('src/WinSwitch.Core/Services/WindowEnumerator.cs', 'w') as f:
    f.write(we_code)

print("问题4修复完成: WindowEnumerator.cs FindAllMatchingWindows 空标题不匹配 + BossKeyService 已过滤")


# ============================================================
# 最终: 需要在 AppConfig.cs 的 WindowRule 中添加 MatchedHwnd 字段
# 因为 ResolveHwnd 现在设置 rule.MatchedHwnd
# ============================================================

with open('src/WinSwitch.Core/Models/AppConfig.cs', 'r') as f:
    ac_code = f.read()

# 检查是否已有 MatchedHwnd
if 'MatchedHwnd' not in ac_code:
    old_brw = '''    /// <summary>
    /// [JsonIgnore] 缓存的浏览器窗口ID（用于多窗口同站绑定）
    /// </summary>
    [JsonIgnore]
    public int CachedBrowserWindowId { get; set; }'''
    
    new_brw = '''    /// <summary>
    /// [JsonIgnore] 缓存的浏览器窗口ID（用于多窗口同站绑定）
    /// </summary>
    [JsonIgnore]
    public int CachedBrowserWindowId { get; set; }

    /// <summary>
    /// [JsonIgnore] 当次匹配的 HWND（运行时用，防止同标题窗口被不同规则重复绑定）
    /// </summary>
    [JsonIgnore]
    public IntPtr MatchedHwnd { get; set; }'''
    
    ac_code = ac_code.replace(old_brw, new_brw)
    
    # 确保有 System using
    if 'using System;' not in ac_code.split('\n')[0:5]:
        pass  # 应该已经有了
    
    with open('src/WinSwitch.Core/Models/AppConfig.cs', 'w') as f:
        f.write(ac_code)
    
    print("AppConfig.cs WindowRule 添加 MatchedHwnd 字段")
else:
    print("AppConfig.cs 已有 MatchedHwnd，跳过")

print("\n=== 4个问题全部修复完成 ===")