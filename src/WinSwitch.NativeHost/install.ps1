# WinSwitch 浏览器扩展安装脚本
# 默认注册到当前用户 HKCU，不需要管理员权限
# 如需注册到全局 HKLM，请使用 -AllUsers，并用管理员权限运行 PowerShell

param(
    # 扩展 ID
    [string]$ExtensionId = "onmdjfmanjjfgiikhohjhknmphchfcjk",

    # 卸载
    [switch]$Uninstall,

    # 注册到 HKLM，全局生效，需要管理员权限
    [switch]$AllUsers
)

$HostName = "com.winswitch.bridge"
$ManifestFileName = "$HostName.json"
$NativeHostExe = "WinSwitch.NativeHost.exe"

# 找到脚本所在目录
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ManifestPath = Join-Path $ScriptDir $ManifestFileName
$ExePath = Join-Path $ScriptDir $NativeHostExe

# 根据参数选择注册表根路径
if ($AllUsers) {
    $RegRoot = "HKLM:"
} else {
    $RegRoot = "HKCU:"
}

$ChromeKey = "$RegRoot\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"
$EdgeKey = "$RegRoot\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"

function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Register-NativeHost {
    param(
        [string]$KeyPath,
        [string]$ManifestPath,
        [string]$BrowserName
    )

    # 创建最终注册表项
    New-Item -Path $KeyPath -Force | Out-Null

    # 设置默认值为 manifest 路径
    # Chrome / Edge Native Messaging 要求默认值指向 manifest json 文件
    Set-Item -Path $KeyPath -Value $ManifestPath

    Write-Host "Registered for $BrowserName : $KeyPath" -ForegroundColor Green
}

function Unregister-NativeHost {
    param(
        [string]$KeyPath,
        [string]$BrowserName
    )

    if (Test-Path $KeyPath) {
        Remove-Item -Path $KeyPath -Force
        Write-Host "Unregistered for $BrowserName : $KeyPath" -ForegroundColor Green
    } else {
        Write-Host "Registry key not found for $BrowserName : $KeyPath" -ForegroundColor Yellow
    }
}

if ($AllUsers -and -not (Test-IsAdministrator)) {
    Write-Host "Error: -AllUsers requires administrator PowerShell." -ForegroundColor Red
    Write-Host "Please run PowerShell as administrator, or remove -AllUsers to install for current user." -ForegroundColor Yellow
    exit 1
}

if ($Uninstall) {
    Write-Host "Uninstalling WinSwitch Native Messaging Host..." -ForegroundColor Yellow

    Unregister-NativeHost -KeyPath $ChromeKey -BrowserName "Chrome"
    Unregister-NativeHost -KeyPath $EdgeKey -BrowserName "Edge"

    Write-Host "Uninstall complete." -ForegroundColor Green
    return
}

Write-Host "Installing WinSwitch Native Messaging Host..." -ForegroundColor Cyan

# 检查 Native Host 程序是否存在
if (-not (Test-Path $ExePath)) {
    Write-Host "Error: Native host executable not found:" -ForegroundColor Red
    Write-Host "  $ExePath" -ForegroundColor Red
    exit 1
}

# 简单校验扩展 ID
if ($ExtensionId -notmatch '^[a-z]{32}$') {
    Write-Host "Warning: Extension ID does not look like a standard Chrome extension ID." -ForegroundColor Yellow
    Write-Host "Current Extension ID: $ExtensionId" -ForegroundColor Yellow
    Write-Host "If this is correct, you can ignore this warning." -ForegroundColor Yellow
}

# 构建 allowed_origins
$ChromeOrigin = "chrome-extension://$ExtensionId/"

# 更新 manifest：设置 path 为绝对路径，设置 allowed_origins
$ManifestObject = [ordered]@{
    name = $HostName
    description = "WinSwitch Browser Bridge - Native Messaging Host"
    path = $ExePath
    type = "stdio"
    allowed_origins = @($ChromeOrigin)
}

$ManifestJson = $ManifestObject | ConvertTo-Json -Depth 10

# 写入 manifest 文件
Set-Content -Path $ManifestPath -Value $ManifestJson -Encoding UTF8

Write-Host "Updated manifest:" -ForegroundColor Green
Write-Host "  $ManifestPath"
Write-Host "Extension ID:" -ForegroundColor Green
Write-Host "  $ExtensionId"
Write-Host "Native Host:" -ForegroundColor Green
Write-Host "  $ExePath"

# 注册 Chrome
Register-NativeHost -KeyPath $ChromeKey -ManifestPath $ManifestPath -BrowserName "Chrome"

# 注册 Edge
Register-NativeHost -KeyPath $EdgeKey -ManifestPath $ManifestPath -BrowserName "Edge"

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Registry scope:" -ForegroundColor Yellow

if ($AllUsers) {
    Write-Host "  All users, HKLM" -ForegroundColor Yellow
} else {
    Write-Host "  Current user, HKCU" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open chrome://extensions or edge://extensions"
Write-Host "  2. Enable Developer mode"
Write-Host "  3. Load unpacked extension folder"
Write-Host "  4. Restart the browser completely"
Write-Host ""
Write-Host "If you need to specify another extension ID, run:" -ForegroundColor Cyan
Write-Host "  .\install.ps1 -ExtensionId YOUR_EXTENSION_ID" -ForegroundColor Cyan
Write-Host ""
Write-Host "If you want to install for all users, run PowerShell as administrator and use:" -ForegroundColor Cyan
Write-Host "  .\install.ps1 -ExtensionId YOUR_EXTENSION_ID -AllUsers" -ForegroundColor Cyan
Write-Host ""
Write-Host "To uninstall:" -ForegroundColor Cyan
Write-Host "  .\install.ps1 -Uninstall" -ForegroundColor Cyan
