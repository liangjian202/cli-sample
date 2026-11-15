# filecli 一键安装脚本（Windows PowerShell）
# 用法: irm https://raw.githubusercontent.com/liangjian202/cli-sample/master/FileCli/scripts/install.ps1 | iex
$ErrorActionPreference = "Stop"

$Repo = "liangjian202/cli-sample"
$BinDir = "$env:LOCALAPPDATA\Programs\filecli"

# 1. 识别架构 -> Release 资产名
# 32 位 PowerShell 进程里 PROCESSOR_ARCHITECTURE 会退化成 x86，真实架构在 PROCESSOR_ARCHITEW6432
$rawArch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
$arch = switch ($rawArch) {
    "AMD64" { "x64" }
    "ARM64" { "arm64" }
    default { throw "不支持的架构: $rawArch" }
}
$asset = "filecli-win-$arch.zip"

# 2. 下载并解压
$tmp = Join-Path $env:TEMP ("filecli-" + [Guid]::NewGuid().ToString("N"))
try {
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $url = "https://github.com/$Repo/releases/latest/download/$asset"
    Write-Host "下载 $url"
    $zip = Join-Path $tmp $asset
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $tmp -Force

    # 3. 安装到用户目录并加入 PATH
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    $dest = Join-Path $BinDir "filecli.exe"
    if (Get-Process filecli -ErrorAction SilentlyContinue) {
        throw "检测到 filecli 正在运行，请先关闭相关进程再重试（否则无法覆盖 $dest）"
    }
    Copy-Item (Join-Path $tmp "filecli.exe") $dest -Force
}
finally {
    # 下载或解压失败时也不留下临时目录
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
$paths = @($userPath -split ";" | Where-Object { $_ })
if ($paths -notcontains $BinDir) {
    [Environment]::SetEnvironmentVariable("Path", (@($paths) + $BinDir) -join ";", "User")
    Write-Host "已将 $BinDir 加入用户 PATH（重开终端生效）"
}

Write-Host "已安装到 $dest"
Write-Host "验证（当前窗口）: & `"$dest`" --help"
Write-Host "验证（重开终端后）: filecli --help"
