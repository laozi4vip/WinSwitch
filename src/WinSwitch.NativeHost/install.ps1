# WinSwitch 浏览器扩展安装脚本
# 需要管理员权限运行
param(
    # 扩展ID（默认为 WinSwitch Browser Bridge 的固定ID）
    [string]$ExtensionId = "onmdjfmanjjfgiikhohjhknmphchfcjk",
    [switch]$Uninstall
)

$HostName = "com.winswitch.bridge"
$ManifestFileName = "$HostName.json"
$NativeHostExe = "WinSwitch.NativeHost.exe"

# 找到脚本所在目录
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ManifestPath = Join-Path $ScriptDir $ManifestFileName
$ExePath = Join-Path $ScriptDir $NativeHostExe

if ($Uninstall) {
    Write-Host "Uninstalling WinSwitch Native Messaging Host..." -ForegroundColor Yellow
    
    $chromeKey = "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"
    if (Test-Path $chromeKey) { Remove-Item $chromeKey -Force }
    
    $edgeKey = "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"
    if (Test-Path $edgeKey) { Remove-Item $edgeKey -Force }
    
    Write-Host "Uninstalled successfully." -ForegroundColor Green
    return
}

Write-Host "Installing WinSwitch Native Messaging Host..." -ForegroundColor Cyan

# 构建 allowed_origins
$chromeOrigin = "chrome-extension://$ExtensionId/"

# 更新 manifest：设置 path 为绝对路径，设置 allowed_origins
$manifest = @{
    name = $HostName
    description = "WinSwitch Browser Bridge - Native Messaging Host"
    path = $ExePath
    type = "stdio"
    allowed_origins = @($chromeOrigin)
} | ConvertTo-Json -Depth 10

Set-Content -Path $ManifestPath -Value $manifest -Encoding UTF8
Write-Host "Updated manifest: Extension ID = $ExtensionId" -ForegroundColor Green

# Chrome 注册表
$chromeKey = "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"
if (-not (Test-Path (Split-Path $chromeKey))) {
    New-Item -Path (Split-Path $chromeKey) -Force | Out-Null
}
New-ItemProperty -Path $chromeKey -Name "(Default)" -Value $ManifestPath -Force | Out-Null
Write-Host "Registered for Chrome" -ForegroundColor Green

# Edge 注册表
$edgeKey = "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"
if (-not (Test-Path (Split-Path $edgeKey))) {
    New-Item -Path (Split-Path $edgeKey) -Force | Out-Null
}
New-ItemProperty -Path $edgeKey -Name "(Default)" -Value $ManifestPath -Force | Out-Null
Write-Host "Registered for Edge" -ForegroundColor Green

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Load the browser extension in chrome://extensions (Developer mode -> Load unpacked -> select WinSwitch.BrowserExtension folder)"
Write-Host "  2. Restart the browser (close all browser windows and reopen)"
Write-Host ""
Write-Host "Note: If you changed the extension ID, re-run with:" -ForegroundColor Cyan
Write-Host "  .\install.ps1 -ExtensionId <your-extension-id>" -ForegroundColor Cyan
