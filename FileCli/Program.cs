using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

if (Console.OutputEncoding.CodePage != 65001)
    Console.OutputEncoding = Encoding.UTF8;

return FileCli.Cli.Run(args);

namespace FileCli
{
    internal static class Cli
    {
        // AOT 兼容：源生成序列化，禁用反射
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = CliJsonContext.Default
        };

        public static int Run(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
            {
                PrintUsage();
                return 0;
            }

            var group = args[0].ToLowerInvariant();
            if (args.Length < 2)
                return UsageError($"'{group}' 缺少子命令（create/delete），运行 filecli --help 查看用法");

            var action = args[1].ToLowerInvariant();

            Options opts;
            try
            {
                opts = ParseOptions(args.Skip(2));
            }
            catch (UsageException ex)
            {
                return UsageError(ex.Message);
            }

            if (opts.Format is not (null or "text" or "json"))
                return UsageError($"--format 仅支持 text 或 json，收到: {opts.Format}");

            try
            {
                return (group, action) switch
                {
                    ("file", "create") => FileCreate(opts),
                    ("file", "delete") => FileDelete(opts),
                    ("dir", "create") => DirCreate(opts),
                    ("dir", "delete") => DirDelete(opts),
                    _ => UsageError($"未知命令 '{group} {action}'，运行 filecli --help 查看用法")
                };
            }
            catch (Exception ex)
            {
                return Emit(opts, $"{group} {action}", opts.Positionals.FirstOrDefault() ?? "", ex.Message, 1);
            }
        }

        // ---------- 四个命令实现 ----------

        private static int FileCreate(Options o)
        {
            var full = Path.GetFullPath(RequirePath(o));
            bool exists = File.Exists(full);

            if (exists && !o.Overwrite)
                return Emit(o, "file create", full, "文件已存在（加 --overwrite 可覆盖）", 1);

            string msg;
            if (o.DryRun)
            {
                msg = exists ? "dry-run: 将覆盖已有文件（未实际执行）" : "dry-run: 将创建文件（未实际执行）";
            }
            else
            {
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(full, o.Content ?? string.Empty, Encoding.UTF8);
                msg = exists ? "文件已覆盖写入" : "文件创建成功（父目录自动创建）";
            }
            return Emit(o, "file create", full, msg, 0);
        }

        private static int FileDelete(Options o)
        {
            var full = Path.GetFullPath(RequirePath(o));

            if (!File.Exists(full))
                return Emit(o, "file delete", full, "文件不存在", 1);

            if (o.DryRun)
                return Emit(o, "file delete", full, "dry-run: 将删除该文件（未实际执行）", 0);

            var fi = new FileInfo(full);
            if (fi.IsReadOnly && !o.Force)
                return Emit(o, "file delete", full, "文件为只读，加 --force 移除只读属性后删除", 1);

            if (fi.IsReadOnly) fi.IsReadOnly = false;
            fi.Delete();
            return Emit(o, "file delete", full, "文件已删除", 0);
        }

        private static int DirCreate(Options o)
        {
            var full = Path.GetFullPath(RequirePath(o));
            bool existed = Directory.Exists(full);

            if (o.DryRun)
                return Emit(o, "dir create", full,
                    existed ? "dry-run: 目录已存在，无需操作" : "dry-run: 将创建目录（含父级）（未实际执行）", 0);

            Directory.CreateDirectory(full); // 幂等：已存在时不报错
            return Emit(o, "dir create", full,
                existed ? "目录已存在" : "目录创建成功（父目录自动创建）", 0);
        }

        private static int DirDelete(Options o)
        {
            var full = Path.GetFullPath(RequirePath(o));

            if (!Directory.Exists(full))
                return Emit(o, "dir delete", full, "目录不存在", 1);

            var (fileCount, dirCount) = CountContents(full);
            bool empty = fileCount + dirCount == 0;

            if (o.DryRun)
                return Emit(o, "dir delete", full,
                    empty ? "dry-run: 将删除空目录（未实际执行）"
                          : $"dry-run: 将递归删除 {fileCount} 个文件、{dirCount} 个子目录（未实际执行，实际执行需 --recursive）", 0);

            if (!empty && !o.Recursive)
                return Emit(o, "dir delete", full,
                    $"目录非空（含 {fileCount} 个文件、{dirCount} 个子目录），删除需要 --recursive", 1);

            Directory.Delete(full, o.Recursive);
            return Emit(o, "dir delete", full,
                empty ? "空目录已删除" : $"已递归删除 {fileCount} 个文件、{dirCount} 个子目录", 0);
        }

        // ---------- 辅助 ----------

        private static (int files, int dirs) CountContents(string dir)
        {
            int files = 0, dirs = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                if (Directory.Exists(entry))
                {
                    dirs++;
                    var (f, d) = CountContents(entry);
                    files += f; dirs += d;
                }
                else files++;
            }
            return (files, dirs);
        }

        private static string RequirePath(Options o)
        {
            if (o.Positionals.Count == 0)
                throw new UsageException("缺少必填参数 <路径>");
            if (o.Positionals.Count > 1)
                throw new UsageException($"最多接受 1 个路径参数，收到 {o.Positionals.Count} 个");
            return o.Positionals[0];
        }

        private static int Emit(Options o, string action, string path, string message, int exitCode)
        {
            bool ok = exitCode == 0;
            if (o.Format == "json")
            {
                var payload = new CliResult(ok, action, path, message);
                Console.Out.WriteLine(JsonSerializer.Serialize(payload, JsonOpts));
            }
            else
            {
                var line = $"{(ok ? "OK" : "FAIL")}  {action}  {path}  -  {message}";
                if (ok) Console.Out.WriteLine(line);
                else Console.Error.WriteLine(line);
            }
            return exitCode;
        }

        private static int UsageError(string message)
        {
            Console.Error.WriteLine($"USAGE-ERROR: {message}");
            Console.Error.WriteLine("运行 filecli --help 查看完整用法。");
            return 2;
        }

        private static Options ParseOptions(IEnumerable<string> args)
        {
            var o = new Options();
            string? pending = null;
            foreach (var a in args)
            {
                if (pending != null)
                {
                    if (pending == "--content") o.Content = a;
                    if (pending == "--format") o.Format = a;
                    pending = null;
                    continue;
                }
                switch (a)
                {
                    case "--dry-run": o.DryRun = true; break;
                    case "--overwrite": o.Overwrite = true; break;
                    case "--force": o.Force = true; break;
                    case "--recursive": o.Recursive = true; break;
                    case "--no-interactive": o.NoInteractive = true; break;
                    case "--content": pending = a; break;
                    case "--format": pending = a; break;
                    default:
                        if (a.StartsWith("--")) throw new UsageException($"未知选项: {a}");
                        o.Positionals.Add(a);
                        break;
                }
            }
            if (pending != null) throw new UsageException($"选项 {pending} 缺少值");
            return o;
        }

        private static void PrintUsage()
        {
            Console.Out.WriteLine("""
                filecli - 文件/目录管理命令行工具（AI-Friendly 设计）

                用法:
                  filecli <group> <action> <路径> [选项]

                命令:
                  file create <路径>     创建文件（父目录自动创建）
                  file delete <路径>     删除文件
                  dir  create <路径>     创建目录（含父级，幂等）
                  dir  delete <路径>     删除目录（非空需 --recursive）

                选项:
                  --content <文本>       file create: 写入的文本内容（UTF-8）
                  --overwrite            file create: 目标已存在时覆盖
                  --recursive            dir delete: 递归删除非空目录
                  --force                file delete: 移除只读属性后删除
                  --dry-run              预览将要执行的操作，不做任何更改
                  --format <text|json>   输出格式，json 适合程序/AI 解析
                  --no-interactive       声明非交互模式（本工具从不等待输入）

                退出码:
                  0  成功
                  1  执行失败（如目标不存在、目录非空、文件只读）
                  2  用法错误（未知命令、缺参数、未知选项）

                示例:
                  filecli file create ./notes/todo.txt --content "第一行" --format json
                  filecli file delete ./notes/todo.txt --dry-run
                  filecli dir create ./logs/2026/11
                  filecli dir delete ./logs --recursive --dry-run
                """);
        }

        private sealed class Options
        {
            public List<string> Positionals { get; } = new();
            public bool DryRun { get; set; }
            public bool Overwrite { get; set; }
            public bool Force { get; set; }
            public bool Recursive { get; set; }
            public bool NoInteractive { get; set; }
            public string? Content { get; set; }
            public string? Format { get; set; }
        }

        private sealed class UsageException(string message) : Exception(message);
    }

    /// <summary>结构化输出模型（AOT 源生成序列化要求具名类型）</summary>
    public sealed record CliResult(bool Ok, string Action, string Path, string Message);

    [JsonSerializable(typeof(CliResult))]
    internal sealed partial class CliJsonContext : JsonSerializerContext { }
}
