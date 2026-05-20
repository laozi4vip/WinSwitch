# WinSwitch 浏览器扩展安装脚本
# 需要管理员权限运行

param([switch]$Uninstall)

$HostName = "com.winswitch.bridge"
$ManifestFileName = "$HostName.json"
$NativeHostExe = "WinSwitch.NativeHost.exe"

# 找到 NativeHost 目录
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ManifestPath = Join-Path $ScriptDir $ManifestFileName
$ExePath = Join-Path $ScriptDir $NativeHostExe

# 更新 manifest 中的 path 为绝对路径
$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$manifest.path = $ExePath
$manifest | ConvertTo-Json | Set-Content $ManifestPath

if ($Uninstall) {
    Write-Host "Uninstalling WinSwitch Native Messaging Host..." -ForegroundColor Yellow
    
    # Chrome
    $chromeKey = "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"
    if (Test-Path $chromeKey) { Remove-Item $chromeKey -Force }
    
    # Edge
    $edgeKey = "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"
    if (Test-Path $edgeKey) { Remove-Item $edgeKey -Force }
    
    Write-Host "Uninstalled successfully." -ForegroundColor Green
    return
}

Write-Host "Installing WinSwitch Native Messaging Host..." -ForegroundColor Cyan

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

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Load the browser extension in chrome://extensions (Developer mode)"
Write-Host "  2. Restart the browser"
