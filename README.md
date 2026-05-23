# WinSwitch / 窗口快捷切换助手

> 快捷键实现窗口"非前台→激活 / 前台→最小化"，支持多窗口独立绑定和老板键隐藏。

## ✨ 功能一览

### 基础功能
- **全局快捷键切换窗口**：按快捷键智能切换窗口前台/最小化状态
  - 窗口在后台 → 激活到前台
  - 窗口在前台 → 最小化
- **固定窗口绑定**：通过窗口拾取器直接绑定窗口句柄（如微信、Excel）
- **规则窗口绑定**：通过进程名 + 标题规则区分同进程多窗口
  - 支持包含、开头匹配、精确匹配、正则表达式
  - 支持分号分隔多关键词（所有关键词必须同时匹配）
- **进程名绑定**：直接按进程名绑定，无需指定窗口标题
- **任务栏快捷键绑定 (TaskbarPin)**：将热键映射到 Windows 任务栏固定程序
  - 支持模拟 Win+1 ~ Win+0（对应任务栏第1~10个固定程序）
  - 基于 SendInput API 实现，兼容性好
  - 自动释放用户按住的修饰键，确保 Win+数字不被干扰
- **老板键**：一键隐藏/恢复所有规则窗口
  - 采用纯进程名匹配，稳定可靠
  - 模式1：仅隐藏窗口
  - 模式2：隐藏窗口 + 任务栏图标
  - 模式3：隐藏窗口 + 任务栏 + Alt+Tab
- **系统托盘常驻**：单击打开主窗口，右键显示菜单
- **开机自启动**：通过注册表实现
- **JSON 配置持久化**：所有规则和设置自动保存
- **F1-F12 单键快捷键**：支持无修饰键的功能键
- **快捷键冲突检测**：设置时实时提示冲突
- **在线更新检查**：主界面一键检查 GitHub 最新版本

### 浏览器扩展联动
- **三种浏览器匹配方式**：

  | 匹配模式 | 是否需要扩展 | 说明 |
  |----------|:----------:|------|
  | 当前标签页标题 | ❌ | 使用 Win32 窗口标题匹配当前活动标签页 |
  | 任意标签页标题 | ✅ | 匹配浏览器中任意标签页的标题（后台标签也能匹配） |
  | URL 匹配 | ✅ | 按标签页 URL 匹配（最精确，支持包含/前缀/精确/正则） |

- **多浏览器/多窗口/多 Profile 同时连接**：支持 Chrome、Edge、Brave 等多个浏览器同时安装扩展
- **智能 HWND 匹配**：浏览器窗口与 Win32 窗口自动关联
  - 4 级回退策略：HWND 直接映射 → 标签页标题 → 规则关键词 → Win32 回退
  - HWND 级硬绑定（CachedBrowserHwnd），提升特定窗口锁定稳定性
- **跨规则 HWND 占用检查**：一个浏览器窗口绑定到某规则后，不会被其他规则重复绑定
- **窗口位置稳定分配**：多个匹配窗口时，按坐标（Top→Left）排序确保首次绑定结果一致

## 🛠 技术栈
- **主程序**：C# + .NET 8 + WPF
- **Win32 API**：SetForegroundWindow, ShowWindow, SetWindowPos, SendInput 等
- **浏览器扩展**：Manifest V3 + Native Messaging
- **Native Host**：C# 控制台程序（stdin/stdout ↔ Named Pipe）
- **安装程序**：Inno Setup 6
- **自包含发布**：无需预装 .NET 运行时

## 📋 系统要求
- Windows 10 21H2+ / Windows 11
- 无需预装 .NET（自包含部署）
- 浏览器扩展功能需要 Chrome 或 Edge 浏览器

## 🚀 安装与使用

### 方式一：安装版（推荐）
1. 从 [GitHub Releases](https://github.com/laozi4vip/WinSwitch/releases) 下载 `WinSwitch-Setup-*.exe`
2. 双击运行安装程序
3. 勾选"开机自启动"（可选）
4. 安装完成后从桌面快捷方式或开始菜单启动

### 方式二：便携版
1. 从 [GitHub Releases](https://github.com/laozi4vip/WinSwitch/releases) 下载 `WinSwitch-*.zip`
2. 解压到任意目录
3. 运行 `WinSwitch.exe`

### 下载文件说明
- `WinSwitch-Setup-*.exe` — Windows 安装版（推荐，自动创建快捷方式、支持开机自启）
- `WinSwitch-*.zip` — 完整压缩包（主程序 + 浏览器扩展 + NativeHost，解压即用）
- `.sha256` — 校验文件

## 📖 使用指南

### 快速开始
1. 运行后程序常驻系统托盘（☀ 图标）
2. 右键托盘图标或主窗口点击"添加规则"
3. 为每个窗口配置快捷键和匹配方式
4. 按快捷键切换窗口，按 `Ctrl+`` 触发老板键

### 四种匹配模式详解

#### 1. 固定窗口绑定 (Fixed)
- **适用场景**：绑定特定窗口（如微信、某个 Excel 文件）
- **操作方式**：点击"拾取窗口"按钮，点击目标窗口即可绑定
- **设置项**：进程名（自动填充）、老板键开关

#### 2. 规则窗口绑定 (Rule)
- **适用场景**：通过标题规则区分同进程多窗口
- **操作方式**：填写进程名 + 标题规则
- **设置项**：进程名、标题规则、浏览器匹配模式、URL 规则、老板键开关
- **标题匹配方式**：包含 / 开头匹配 / 精确匹配 / 正则表达式
- **多关键词**：用分号分隔，所有关键词必须同时匹配

#### 3. 进程名绑定 (ProcessName)
- **适用场景**：按进程名直接绑定，无需指定窗口标题
- **操作方式**：填写进程名（如 `chrome`、`notepad`）
- **设置项**：进程名、老板键开关

#### 4. 任务栏快捷键绑定 (TaskbarPin)
- **适用场景**：用自定义热键触发 Windows 任务栏固定程序
- **操作方式**：设置任务栏序号（1~10，对应 Win+1 到 Win+0）
- **设置项**：任务栏序号
- **行为说明**：
  - 程序未启动 → 按 Win+数字启动程序
  - 程序在后台 → 按 Win+数字激活窗口
  - 程序在前台 → 按 Win+数字最小化窗口
  - 上述行为由 Windows 原生 Win+数字快捷键实现，WinSwitch 仅负责模拟按键

### 浏览器扩展安装（可选）

如需使用"任意标签页标题匹配"或"URL 匹配"功能：

1. 进入程序目录下的 `BrowserExt` 文件夹
2. 以管理员身份运行 `install.ps1`（注册 Native Messaging Host）
3. 打开 Chrome/Edge → `chrome://extensions` → 开发者模式 → 加载已解压的扩展
4. 选择 `BrowserExt\WinSwitch.BrowserExtension` 目录
5. 重启浏览器

> 扩展会自动连接 WinSwitch 主程序，实时同步浏览器窗口和标签页信息。

### 老板键使用

| 模式 | 行为 | 适用场景 |
|------|------|----------|
| 模式1 | 仅隐藏窗口 | 快速切换，任务栏仍可见 |
| 模式2 | 隐藏窗口 + 任务栏图标 | 完全隐藏，Alt+Tab 仍可见 |
| 模式3 | 隐藏窗口 + 任务栏 + Alt+Tab | 最高级别隐藏，程序完全不可见 |

- 默认快捷键：`Ctrl+`` （反引号键）
- 再次按老板键恢复所有隐藏窗口

## ⌨️ 快捷键设置
- **组合键**：按下修饰键（Ctrl/Alt/Shift/Win）+ 其他键，自动识别
- **功能键**：F1-F12 无需修饰键，单独按下即可设置
- **冲突检测**：设置时实时提示是否与其他规则冲突

## 📁 项目结构

```
WinSwitch/
├── src/
│   ├── WinSwitch.Core/          # 核心逻辑层
│   │   ├── Models/              # 数据模型（含V2浏览器模型）
│   │   ├── Services/            # 业务服务（含BrowserBridgeService）
│   │   └── Interop/             # Win32 P/Invoke
│   ├── WinSwitch.UI/            # WPF 界面层
│   │   └── Views/               # 窗口和对话框
│   ├── WinSwitch.BrowserExtension/  # V2 浏览器扩展
│   │   ├── manifest.json        # 扩展清单
│   │   └── background.js        # 数据收集与发送
│   ├── WinSwitch.NativeHost/    # V2 Native Messaging Host
│   │   ├── Program.cs           # 消息转发（浏览器↔主程序）
│   │   └── install.ps1          # 注册脚本
│   └── WinSwitch.Tests/         # 单元测试
├── installer/                   # Inno Setup 安装脚本
├── .github/workflows/           # CI/CD
└── WinSwitch.sln
```

## 📄 配置文件

配置路径: `%APPDATA%\WinSwitch\config.json`

主要字段：
- `matchMode`: 匹配模式（Fixed/Rule/ProcessName/TaskbarPin）
- `processName`: 进程名
- `titlePattern`: 标题规则
- `taskbarSlot`: 任务栏序号（TaskbarPin 模式，1~10）
- `bossKeyEnabled`: 是否启用老板键
- `browserMatchMode`: 浏览器匹配模式（ActiveTabTitle/AnyTabTitle/AnyTabUrl）
- `urlPattern`: URL 匹配规则
- `urlMatchType`: URL 匹配方式（Contains/StartsWith/Exact/Regex）
- `cachedBrowserHwnd`: 浏览器窗口 HWND 硬绑定

## 📝 版本历史

| 版本 | 内容 |
|------|------|
| V1 | 快捷键切换 + 固定/规则窗口绑定 + 老板键三模式 + 托盘 + JSON 配置 |
| V2 | 浏览器扩展联动 + 任意标签页匹配 + URL 匹配 + 多浏览器支持 + 进程名绑定 + 任务栏快捷键 + HWND 硬绑定 + 跨规则占用检查 + UI 优化 + 在线更新 + 安装版 |

## 📜 许可证

MIT License
