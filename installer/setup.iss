; WinSwitch Inno Setup 安装脚本
; 用于构建 Windows 安装版 (.exe)

#define AppName "WinSwitch"
#define AppVersion "2.1.9"
#define AppPublisher "laozi4vip"
#define AppURL "https://github.com/laozi4vip/WinSwitch"
#define AppExeName "WinSwitch.exe"

[Setup]
AppId={{B8F3D7A1-2C4E-4F6A-9D8B-1E3F5A7C9E2D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.\output
OutputBaseFilename=WinSwitch-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\WinSwitch.UI\sun.ico
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "开机自启动"; GroupDescription: "附加选项"

[Files]
; 主程序（从 publish 目录）
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NativeHost
Source: "..\publish-nativehost\WinSwitch.NativeHost.exe"; DestDir: "{app}\BrowserExt"; Flags: ignoreversion
Source: "..\publish-nativehost\Newtonsoft.Json.dll"; DestDir: "{app}\BrowserExt"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\src\WinSwitch.NativeHost\com.winswitch.bridge.json"; DestDir: "{app}\BrowserExt"; Flags: ignoreversion
Source: "..\src\WinSwitch.NativeHost\install.ps1"; DestDir: "{app}\BrowserExt"; Flags: ignoreversion
; 浏览器扩展
Source: "..\src\WinSwitch.BrowserExtension\manifest.json"; DestDir: "{app}\BrowserExt\WinSwitch.BrowserExtension"; Flags: ignoreversion
Source: "..\src\WinSwitch.BrowserExtension\background.js"; DestDir: "{app}\BrowserExt\WinSwitch.BrowserExtension"; Flags: ignoreversion
Source: "..\src\WinSwitch.BrowserExtension\icon16.png"; DestDir: "{app}\BrowserExt\WinSwitch.BrowserExtension"; Flags: ignoreversion
Source: "..\src\WinSwitch.BrowserExtension\icon48.png"; DestDir: "{app}\BrowserExt\WinSwitch.BrowserExtension"; Flags: ignoreversion
Source: "..\src\WinSwitch.BrowserExtension\icon128.png"; DestDir: "{app}\BrowserExt\WinSwitch.BrowserExtension"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Registry]
; 开机自启动
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
