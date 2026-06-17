# 环刀法压实度检测工具 — 发布脚本
# 用法: .\build.ps1

$ErrorActionPreference = "Stop"
$Version = "1.1.0"
$OutDir = Join-Path $PSScriptRoot "dist\RingKnifeDetector-v$Version-win-x64"
$ZipPath = Join-Path $PSScriptRoot "dist\RingKnifeDetector-v$Version-win-x64.zip"
$Project = Join-Path $PSScriptRoot "RingKnifeDetector\RingKnifeDetector\RingKnifeDetector.csproj"
$UsageSrc = Join-Path $PSScriptRoot "USER_GUIDE.md"
$UsageTxt = Join-Path $OutDir "使用说明.txt"

Write-Host ">>> 停止运行中的程序..."
Get-Process -Name "RingKnifeDetector" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host ">>> 发布 $Version (win-x64, self-contained)..."
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $OutDir

Write-Host ">>> 复制使用说明..."
Copy-Item $UsageSrc $UsageTxt -Force

Write-Host ">>> 打包 zip..."
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path $OutDir -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "完成!"
Write-Host "  程序目录: $OutDir"
Write-Host "  压缩包:   $ZipPath"
Write-Host "  运行:     $OutDir\RingKnifeDetector.exe"
