---
name: dir-delete
description: 用 filecli 删除文件夹/目录。当用户明确要求删除目录时使用。非空目录需要 --recursive，是危险操作，务必先 --dry-run 预览并向用户确认。
---

# dir-delete（删除目录）

删除指定目录。空目录可直接删除；非空目录必须加 `--recursive` 递归删除。

## 命令

```
filecli dir delete <路径> [--recursive] [--dry-run] [--format json]
```

## 参数说明

| 参数 | 作用 |
|------|------|
| `<路径>` | 必填，要删除的目录路径 |
| `--recursive` | 目录非空时递归删除全部内容；不加此参数且目录非空会失败 |
| `--dry-run` | 预览操作：报告将删除的文件数和子目录数，不实际执行 |
| `--format json` | 以 JSON 输出结果 |

## 示例

```
filecli dir delete ./logs --dry-run
filecli dir delete ./logs --recursive --format json
filecli dir delete ./empty-dir
```

## 注意（危险操作流程）

1. 删除前**必须**先带 `--dry-run` 执行一次——它会报告目录里有多少文件和子目录。
2. 把 dry-run 的统计结果展示给用户，获得明确确认后才去掉 `--dry-run` 真正执行。
3. 目录非空且未加 `--recursive` 时命令失败（退出码 1），不要盲目重试，先加 `--dry-run` 查看内容规模。
4. 递归删除不可恢复（本工具不进回收站），对大目录、深层目录要格外谨慎。
