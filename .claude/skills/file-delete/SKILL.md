---
name: file-delete
description: 用 filecli 删除文件。当用户明确要求删除某个文件时使用。这是危险操作，务必先用 --dry-run 预览并向用户确认。
---

# file-delete（删除文件）

删除指定路径的单个文件。只读文件需加 `--force`。

## 命令

```
filecli file delete <路径> [--dry-run] [--force] [--format json]
```

## 参数说明

| 参数 | 作用 |
|------|------|
| `<路径>` | 必填，要删除的文件路径 |
| `--dry-run` | 预览将要删除的文件，不实际执行 |
| `--force` | 文件为只读时，先移除只读属性再删除 |
| `--format json` | 以 JSON 输出结果 |

## 示例

```
filecli file delete ./notes/todo.txt --dry-run
filecli file delete ./notes/todo.txt --format json
filecli file delete ./old.log --force
```

## 注意（危险操作流程）

1. 删除前**必须**先带 `--dry-run` 执行一次，确认目标正确。
2. 向用户展示将要删除的路径，获得明确确认后才真正执行（去掉 `--dry-run` 再跑）。
3. 文件不存在时退出码为 1，输出提示"文件不存在"，无需重试。
4. 删除不可恢复（本工具不进回收站），对批量或重要文件操作要格外谨慎。
