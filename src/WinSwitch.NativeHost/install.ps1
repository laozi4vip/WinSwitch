# WinSwitch 浏览器扩展安装脚本
# 需要管理员权限运行
param(
    [string]$ExtensionId,
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

# 如果没有提供扩展ID，提示用户
if (-not $ExtensionId) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  需要提供浏览器扩展ID" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "获取扩展ID的步骤：" -ForegroundColor White
    Write-Host "  1. 在 Chrome/Edge 中打开 chrome://extensions" -ForegroundColor White
    Write-Host "  2. 开启'开发者模式'" -ForegroundColor White
    Write-Host "  3. 点击'加载已解压的扩展'，选择 WinSwitch.BrowserExtension 目录" -ForegroundColor White
    Write-Host "  4. 加载后，扩展卡片上会显示一串字母数字ID（如：abcdefghijklmnopqrstuvwxyz）" -ForegroundColor White
    Write-Host "  5. 复制该ID" -ForegroundColor White
    Write-Host ""
    Write-Host "然后重新运行此脚本：" -ForegroundColor White
    Write-Host "  .\install.ps1 -ExtensionId <你的扩展ID>" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "示例：" -ForegroundColor White
    Write-Host "  .\install.ps1 -ExtensionId abcdefghijklmnopqrstuvwxyz" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "注意：每次重新加载扩展后ID可能变化，需要重新运行此脚本" -ForegroundColor Red
    return
}

# 构建 allowed_origins
$chromeOrigin = "chrome-extension://$ExtensionId/"
$edgeOrigin = "chrome-extension://$ExtensionId/"  # Edge 也用 chromium 格式

# 更新 manifest
$manifest = @{
    name = $HostName
    description = "WinSwitch Browser Bridge — Native Messaging Host"
    path = $ExePath
    type = "stdio"
    allowed_origins = @($chromeOrigin)
} | ConvertTo-Json -Depth 10

Set-Content -Path $ManifestPath -Value $manifest -Encoding UTF8
Write-Host "Updated manifest with Extension ID: $ExtensionId" -ForegroundColor Green

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
Write-Host "  1. Restart the browser (close all browser windows and reopen)"
Write-Host "  2. The extension should now be able to connect to the native host"
