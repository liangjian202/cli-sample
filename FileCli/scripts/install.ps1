# filecli 一键安装脚本（Windows PowerShell）
# 用法: irm https://raw.githubusercontent.com/liangjian202/cli-sample/main/FileCli/scripts/install.ps1 | iex
$ErrorActionPreference = "Stop"

$Repo = "liangjian202/cli-sample"   # 发布后替换为实际 GitHub 仓库
$BinDir = "$env:LOCALAPPDATA\Programs\filecli"

# 1. 识别架构 -> Release 资产名
$arch = switch ($env:PROCESSOR_ARCHITECTURE) {
    "AMD64"  { "x64" }
    "ARM64"  { "arm64" }
    default  { throw "不支持的架构: $env:PROCESSOR_ARCHITECTURE" }
}
$asset = "filecli-win-$arch.zip"

# 2. 下载并解压
$tmp = Join-Path $env:TEMP ("filecli-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
$url = "https://github.com/$Repo/releases/latest/download/$asset"
Write-Host "下载 $url"
$zip = Join-Path $tmp $asset
Invoke-WebRequest -Uri $url -OutFile $zip
Expand-Archive -Path $zip -DestinationPath $tmp -Force

# 3. 安装到用户目录并加入 PATH
New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
Copy-Item (Join-Path $tmp "filecli.exe") $BinDir -Force
Remove-Item $tmp -Recurse -Force

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$BinDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$BinDir", "User")
    Write-Host "已将 $BinDir 加入用户 PATH（重开终端生效）"
}

Write-Host "已安装到 $BinDir\filecli.exe"
Write-Host "验证: filecli --help"
