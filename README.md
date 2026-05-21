# WinSwitch / 窗口快捷切换助手

> 快捷键实现窗口"非前台→激活 / 前台→最小化"，支持多窗口独立绑定和老板键隐藏。

## ✨ 功能

### V1 基础功能

- **全局快捷键切换窗口**：按快捷键智能切换窗口前台/最小化状态
  - 窗口在后台 → 激活到前台
  - 窗口在前台 → 最小化
- **固定窗口绑定**：通过窗口拾取器直接绑定窗口句柄（如微信、Excel）
- **规则窗口绑定**：通过进程名 + 标题规则区分同进程多窗口
  - 支持包含、开头匹配、精确匹配、正则表达式
  - 支持分号分隔多关键词（任一匹配即可）
- **老板键**：一键隐藏/恢复所有规则窗口
  - 模式1：仅隐藏窗口
  - 模式2：隐藏窗口 + 任务栏图标
  - 模式3：隐藏窗口 + 任务栏 + Alt+Tab
- **系统托盘常驻**：单击打开主窗口，右键显示菜单
- **开机自启动**：通过注册表实现
- **JSON 配置持久化**：所有规则和设置自动保存
- **F1-F12 单键快捷键**：支持无修饰键的功能键
- **快捷键冲突检测**：设置时实时提示冲突

### V2 新增功能

- **浏览器扩展联动**：通过 Chrome/Edge 扩展获取标签页信息，精确控制浏览器窗口
- **三种浏览器匹配方式**：
  - **当前标签页标题匹配**：匹配浏览器当前活动标签页标题（无需扩展）
  - **任意标签页标题匹配**：匹配浏览器任意标签页标题（需扩展）
  - **URL 匹配**：匹配浏览器任意标签页 URL（需扩展，最精确）
- **多浏览器/多窗口/多 Profile 同时连接**：支持 Chrome、Edge、Brave 等多个浏览器同时安装扩展
- **智能 HWND 匹配**：浏览器窗口与 Win32 窗口自动关联（位置→标题→唯一窗口三级策略）
- **4 级回退策略**：HWND 映射 → 标签页标题 → 规则关键词 → Win32 回退
- **在线更新检查**：主界面一键检查 GitHub 最新版本
- **作者信息**：主界面链接到项目主页

## 🛠 技术栈

- **主程序**：C# + .NET 8 + WPF
- **Win32 API**：SetForegroundWindow, ShowWindow, SetWindowPos 等
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

1. 下载 `WinSwitch-Setup-*.exe`
2. 双击运行安装程序
3. 勾选"开机自启动"（可选）
4. 安装完成后从桌面快捷方式或开始菜单启动

### 方式二：便携版

1. 下载 `WinSwitch-*.zip`
2. 解压到任意目录
3. 运行 `WinSwitch.exe`

### 基本使用

1. 运行后程序常驻系统托盘（☀ 图标）
2. 右键托盘图标或主窗口添加规则
3. 为每个窗口配置快捷键和匹配方式
4. 按快捷键切换窗口，按 `Ctrl+`` 触发老板键

### 浏览器扩展安装（V2 可选功能）

如需使用"任意标签页标题匹配"或"URL 匹配"功能：

1. 进入程序目录下的 `BrowserExt` 文件夹
2. 以管理员身份运行 `install.ps1`（注册 Native Messaging Host）
3. 打开 Chrome/Edge → `chrome://extensions` → 开发者模式 → 加载已解压的扩展
4. 选择 `BrowserExt\WinSwitch.BrowserExtension` 目录
5. 重启浏览器

> 扩展会自动连接 WinSwitch 主程序，实时同步浏览器窗口和标签页信息。

### 浏览器扩展匹配模式说明

| 匹配模式 | 是否需要扩展 | 说明 |
|----------|:----------:|------|
| 当前标签页标题 | ❌ | 使用 Win32 窗口标题匹配当前活动标签页 |
| 任意标签页标题 | ✅ | 匹配浏览器中任意标签页的标题（后台标签也能匹配） |
| URL 匹配 | ✅ | 按标签页 URL 匹配（最精确，支持包含/前缀/精确/正则） |

### 老板键模式

| 模式 | 行为 |
|------|------|
| 模式1 | 仅隐藏窗口 |
| 模式2 | 隐藏窗口 + 任务栏图标 |
| 模式3 | 隐藏窗口 + 任务栏 + Alt+Tab |

## ⌨️ 快捷键设置

- **组合键**：按下修饰键（Ctrl/Alt/Shift/Win）+ 其他键，自动识别
- **功能键**：F1-F12 无需修饰键，单独按下即可设置
- **冲突检测**：设置时实时提示是否与其他规则冲突

## 📁 项目结构

```
WinSwitch/
├── src/
│   ├── WinSwitch.Core/           # 核心逻辑层
│   │   ├── Models/               # 数据模型（含V2浏览器模型）
│   │   ├── Services/             # 业务服务（含BrowserBridgeService）
│   │   └── Interop/              # Win32 P/Invoke
│   ├── WinSwitch.UI/             # WPF 界面层
│   │   └── Views/                # 窗口和对话框
│   ├── WinSwitch.BrowserExtension/  # V2 浏览器扩展
│   │   ├── manifest.json         # 扩展清单
│   │   └── background.js         # 数据收集与发送
│   ├── WinSwitch.NativeHost/     # V2 Native Messaging Host
│   │   ├── Program.cs            # 消息转发（浏览器↔主程序）
│   │   └── install.ps1           # 注册脚本
│   └── WinSwitch.Tests/          # 单元测试
├── installer/                    # Inno Setup 安装脚本
├── .github/workflows/            # CI/CD
└── WinSwitch.sln
```

## 📄 配置文件

配置路径: `%APPDATA%\WinSwitch\config.json`

V2 新增字段：
- `browserMatchMode`: 浏览器匹配模式（ActiveTabTitle/AnyTabTitle/AnyTabUrl）
- `urlPattern`: URL 匹配规则
- `urlMatchType`: URL 匹配方式（Contains/StartsWith/Exact/Regex）

## 📝 版本历史

| 版本 | 内容 |
|------|------|
| V1 | 快捷键切换 + 固定/规则窗口绑定 + 老板键三模式 + 托盘 + JSON 配置 |
| V2 | 浏览器扩展联动 + 任意标签页匹配 + URL 匹配 + 多浏览器支持 + 在线更新 + 安装版 |

## 📜 许可证

MIT License
