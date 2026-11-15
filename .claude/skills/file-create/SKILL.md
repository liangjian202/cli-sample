---
name: file-create
description: 用 filecli 创建文件。当用户要求新建文件、把文本写入新文件、或需要覆盖写已有文件时使用。
---

# file-create（创建文件）

创建指定路径的文件，可选写入文本内容。父目录不存在时自动创建。

## 命令

```
filecli file create <路径> [--content "文本"] [--overwrite] [--dry-run] [--format json]
```

## 参数说明

| 参数 | 作用 |
|------|------|
| `<路径>` | 必填，目标文件的完整或相对路径 |
| `--content "文本"` | 写入文件的文本内容（UTF-8），省略则创建空文件 |
| `--overwrite` | 目标已存在时覆盖写入；不加此参数且文件已存在会失败 |
| `--dry-run` | 预览操作但不实际执行 |
| `--format json` | 以 JSON 输出结果，便于程序解析 |

## 示例

```
filecli file create ./notes/todo.txt --content "买菜" --format json
filecli file create ./logs/app.log
filecli file create ./config.json --content "{}" --overwrite
```

## 注意

- 文件已存在且未加 `--overwrite` 时命令失败（退出码 1），输出会提示。覆盖已有文件前，先向用户确认。
- 所有输出走 stdout，错误走 stderr，退出码：0 成功、1 执行失败、2 用法错误。
- 本命令从不等待交互输入，可安全地在脚本和 AI Agent 中使用。
