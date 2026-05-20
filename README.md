# WinSwitch / 窗口快捷切换助手

> 快捷键实现窗口"非前台→激活 / 前台→最小化"，支持多窗口独立绑定和老板键隐藏。

## ✨ 功能

- **全局快捷键切换窗口**：按快捷键智能切换窗口前台/最小化状态
- **固定窗口绑定**：通过窗口句柄绑定微信/Excel等独立程序
- **规则窗口绑定**：通过进程名+标题规则区分同浏览器多窗口
- **老板键**：一键隐藏/恢复窗口（支持三种模式）
- **系统托盘常驻**：单击打开主窗口，右键显示菜单
- **开机自启动**：通过注册表实现
- **JSON配置持久化**：所有规则和设置自动保存

## 🛠 技术栈

- C# + .NET 8 + WPF
- Win32 API (SetForegroundWindow, ShowWindow, SetWindowPos, etc.)
- 自包含发布（无需预装 .NET 运行时）

## 📋 系统要求

- Windows 10 21H2+ / Windows 11
- 无需预装 .NET（自包含部署）

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
│   ├── WinSwitch.Core/       # 核心逻辑层
│   │   ├── Models/           # 数据模型
│   │   ├── Services/         # 业务服务
│   │   └── Interop/          # Win32 P/Invoke
│   ├── WinSwitch.UI/         # WPF 界面层
│   │   ├── Views/            # 窗口和对话框
│   │   └── ViewModels/       # 视图模型
│   └── WinSwitch.Tests/      # 单元测试
├── .github/workflows/        # CI/CD
└── WinSwitch.sln
```

## 📄 配置文件

配置路径: `%APPDATA%\WinSwitch\config.json`

## 📝 版本规划

| 版本 | 内容 |
|------|------|
| V1 (MVP) | 快捷键切换 + 固定/规则窗口绑定 + 老板键三模式 + 托盘 + JSON配置 |
| V2 | 配置导入导出 + 多配置文件 + SQLite存储 + 鼠标拾取窗口 |
| V3 | URL特征识别 + UI Automation增强 + 浏览器扩展联动 |

## 📜 许可证

MIT License
