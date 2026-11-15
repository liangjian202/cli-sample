#!/usr/bin/env bash
# filecli 一键安装脚本（macOS / Linux）
# 用法: curl -fsSL https://raw.githubusercontent.com/liangjian202/cli-sample/main/FileCli/scripts/install.sh | bash
set -euo pipefail

REPO="liangjian202/cli-sample"   # 发布后替换为实际 GitHub 仓库
BIN_DIR="${FILECLI_INSTALL_DIR:-$HOME/.local/bin}"

# 1. 识别平台 → Release 资产名
os="$(uname -s)"
arch="$(uname -m)"
case "$os" in
  Linux*)  platform="linux" ;;
  Darwin*) platform="osx" ;;
  *) echo "不支持的操作系统: $os" >&2; exit 1 ;;
esac
case "$arch" in
  x86_64)  platform="${platform}-x64" ;;
  aarch64|arm64) platform="${platform}-arm64" ;;
  *) echo "不支持的架构: $arch" >&2; exit 1 ;;
esac

# 2. 下载并解压到临时目录
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
url="https://github.com/${REPO}/releases/latest/download/filecli-${platform}.tar.gz"
echo "下载 $url"
curl -fsSL "$url" | tar xz -C "$tmp"

# 3. 安装到用户 bin 目录
mkdir -p "$BIN_DIR"
install -m 755 "$tmp/filecli" "$BIN_DIR/filecli"
echo "已安装到 $BIN_DIR/filecli"
echo "$BIN_DIR" | grep -q "$PATH" || echo "提示: 请将 $BIN_DIR 加入 PATH（写入 ~/.bashrc 或 ~/.zshrc）"
echo "验证: filecli --help"
