---
name: dir-create
description: 用 filecli 创建文件夹/目录。当用户要求新建文件夹、创建多级目录结构时使用。
---

# dir-create（创建目录）

创建目录，父目录不存在时一并自动创建。命令是幂等的：目录已存在时不报错，正常返回。

## 命令

```
filecli dir create <路径> [--dry-run] [--format json]
```

## 参数说明

| 参数 | 作用 |
|------|------|
| `<路径>` | 必填，要创建的目录路径，可包含多级父目录 |
| `--dry-run` | 预览操作但不实际执行 |
| `--format json` | 以 JSON 输出结果 |

## 示例

```
filecli dir create ./logs/2026/11
filecli dir create ./output --format json
filecli dir create ./logs/2026/11 --dry-run
```

## 注意

- 可以一次创建多级：`./a/b/c` 中 a、b 不存在时会被一并创建。
- 目录已存在时返回成功（退出码 0），消息为"目录已存在"，不需要提前判断是否存在。
- 输出走 stdout，退出码：0 成功、1 执行失败、2 用法错误。
