// postinstall：按当前平台从 GitHub Releases 下载对应的 Native AOT 二进制
// 模式同 esbuild：npm 只负责分发，真正的可执行文件按平台按需下载
import { createWriteStream, existsSync, mkdirSync, chmodSync, rmSync } from "node:fs";
import { get } from "node:https";
import { platform, arch } from "node:process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const REPO = "liangjian202/cli-sample"; // 发布后替换为实际 GitHub 仓库
const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

const platformMap = {
  "win32-x64": { rid: "win-x64", file: "filecli-win-x64.zip", exe: "filecli.exe" },
  "win32-arm64": { rid: "win-arm64", file: "filecli-win-arm64.zip", exe: "filecli.exe" },
  "linux-x64": { rid: "linux-x64", file: "filecli-linux-x64.tar.gz", exe: "filecli" },
  "linux-arm64": { rid: "linux-arm64", file: "filecli-linux-arm64.tar.gz", exe: "filecli" },
  "darwin-arm64": { rid: "osx-arm64", file: "filecli-osx-arm64.tar.gz", exe: "filecli" },
  "darwin-x64": { rid: "osx-x64", file: "filecli-osx-x64.tar.gz", exe: "filecli" },
};

const key = `${platform}-${arch}`;
const target = platformMap[key];
if (!target) {
  console.error(`filecli: 不支持的平台 ${key}`);
  process.exit(0); // 不阻塞安装，运行时报错即可
}

const binDir = path.join(root, "bin");
const dest = path.join(binDir, target.exe);
if (existsSync(dest)) {
  console.log(`filecli: 已存在，跳过下载`);
  process.exit(0);
}
mkdirSync(binDir, { recursive: true });

const url = `https://github.com/${REPO}/releases/latest/download/${target.file}`;
const tmp = dest + ".download";
console.log(`filecli: 下载 ${url}`);

function download(url, redirects = 5) {
  return new Promise((resolve, reject) => {
    get(url, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location && redirects > 0) {
        return resolve(download(res.headers.location, redirects - 1));
      }
      if (res.statusCode !== 200) {
        return reject(new Error(`HTTP ${res.statusCode}`));
      }
      res.pipe(createWriteStream(tmp)).on("finish", resolve).on("error", reject);
    }).on("error", reject);
  });
}

try {
  await download(url);
  if (target.file.endsWith(".zip")) {
    // 最小 zip 提取（单文件条目），避免依赖第三方库
    const { execSync } = await import("node:child_process");
    if (platform === "win32") {
      execSync(`powershell -NoProfile -Command "Expand-Archive -Path '${tmp}' -DestinationPath '${binDir}' -Force"`);
    } else {
      execSync(`unzip -o ${tmp} -d ${binDir}`);
    }
  } else {
    const { execSync } = await import("node:child_process");
    execSync(`tar xzf ${tmp} -C ${binDir}`);
  }
  if (platform !== "win32") chmodSync(dest, 0o755);
  console.log(`filecli: 安装完成 -> ${dest}`);
} catch (e) {
  console.error(`filecli: 下载失败 (${e.message})，请改用安装脚本安装`);
} finally {
  rmSync(tmp, { force: true });
}
