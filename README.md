# WinSwitch / 窗口快捷切换助手

> 快捷键实现窗口"非前台→激活 / 前台→最小化"，支持多窗口独立绑定和老板键隐藏。

## ✨ 功能

- **全局快捷键切换窗口**：按快捷键智能切换窗口前台/最小化状态
- **固定窗口绑定**：通过窗口句柄绑定微信/Excel等独立程序
- **规则窗口绑定**：通过进程名+标题规则区分同浏览器多窗口
- **浏览器扩展联动（V2新增）**：通过浏览器扩展获取标签页信息，精确控制浏览器窗口
- **三种浏览器匹配方式（V2新增）**：
  - 当前标签页标题匹配（无需扩展）
  - 任意标签页标题匹配（需扩展）
  - URL匹配（需扩展，最精确）
- **老板键**：一键隐藏/恢复窗口（支持三种模式）
- **系统托盘常驻**：单击打开主窗口，右键显示菜单
- **开机自启动**：通过注册表实现
- **JSON配置持久化**：所有规则和设置自动保存
- **F1-F12 单键快捷键**：支持无修饰键的功能键
- **快捷键冲突检测**：设置时实时提示冲突

## 🛠 技术栈

- C# + .NET 8 + WPF
- Win32 API (SetForegroundWindow, ShowWindow, SetWindowPos, etc.)
- Chrome/Edge Extension + Native Messaging（V2）
- 自包含发布（无需预装 .NET 运行时）

## 📋 系统要求

- Windows 10 21H2+ / Windows 11
- 无需预装 .NET（自包含部署）
- 浏览器扩展功能需要 Chrome 或 Edge 浏览器

## 🚀 快速开始

### 从 Release 下载

前往 [Releases](https://github.com/laozi4vip/WinSwitch/releases) 下载最新版本 ZIP，解压后运行 `WinSwitch.exe`。

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/laozi4vip/WinSwitch.git
cd WinSwitch

# 还原依赖
dotnet restore

# 构建
dotnet build --configuration Release

# 发布自包含单文件
dotnet publish src/WinSwitch.UI/WinSwitch.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## ⌨️ 使用方法

1. 运行 WinSwitch.exe，程序常驻系统托盘
2. 右键托盘图标或主窗口添加规则
3. 为每个窗口配置快捷键和匹配方式
4. 按快捷键切换窗口，按 `Ctrl+`` 触发老板键

### 浏览器扩展安装（V2新增）

如需使用"任意标签页标题匹配"或"URL匹配"功能：

1. 以管理员身份运行 `install.ps1` 注册 Native Messaging Host
2. 打开 Chrome/Edge → `chrome://extensions` → 开发者模式 → 加载已解压的扩展
3. 选择 `WinSwitch.BrowserExtension` 目录
4. 重启浏览器

扩展会自动连接 WinSwitch 主程序，实时同步浏览器窗口和标签页信息。

### 老板键模式

| 模式 | 行为 |
|------|------|
| 模式1 | 仅隐藏窗口 |
| 模式2 | 隐藏窗口+任务栏图标 |
| 模式3 | 隐藏窗口+任务栏+Alt+Tab |

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
├── .github/workflows/            # CI/CD
└── WinSwitch.sln
```

## 📄 配置文件

配置路径: `%APPDATA%\WinSwitch\config.json`

V2 新增字段：
- `browserMatchMode`: 浏览器匹配模式（ActiveTabTitle/AnyTabTitle/AnyTabUrl）
- `urlPattern`: URL 匹配规则
- `urlMatchType`: URL 匹配方式（Contains/StartsWith/Exact/Regex）

## 📝 版本规划

| 版本 | 内容 | 状态 |
|------|------|------|
| V1 (MVP) | 快捷键切换 + 固定/规则窗口绑定 + 老板键三模式 + 托盘 + JSON配置 | ✅ 已完成 |
| V2 | 浏览器扩展联动 + 任意标签页匹配 + URL匹配 + Native Messaging | 🔄 开发中 |
| V3 | 配置导入导出 + SQLite存储 + UI Automation增强 | 📋 规划中 |

## 📜 许可证

MIT License
