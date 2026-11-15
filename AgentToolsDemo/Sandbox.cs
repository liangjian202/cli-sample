namespace AgentToolsDemo;

/// <summary>
/// 所有文件操作都被限制在这个目录内。
/// 护栏在运行时，不在模型 —— 模型可以随便传路径，越界一律拒绝。
/// </summary>
public static class Sandbox
{
    public static string Root { get; } = Path.Combine(Environment.CurrentDirectory, "sandbox");

    public static void Reset()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
        Directory.CreateDirectory(Root);
    }

    /// <summary>把模型给的相对路径解析为绝对路径；绝对路径和 .. 越界都拒绝。</summary>
    public static string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("path 不能为空");
        }
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"只接受相对路径，收到：{relativePath}");
        }

        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        var rootWithSeparator = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"路径越界，已拒绝：{relativePath}");
        }
        return full;
    }

    public static string Relative(string fullPath) =>
        Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    public static string Tree()
    {
        var lines = new List<string> { "sandbox/" };

        void Walk(string dir, string indent)
        {
            foreach (var sub in Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.Ordinal))
            {
                lines.Add($"{indent}{Path.GetFileName(sub)}/");
                Walk(sub, indent + "  ");
            }
            foreach (var file in Directory.GetFiles(dir).OrderBy(x => x, StringComparer.Ordinal))
            {
                lines.Add($"{indent}{Path.GetFileName(file)}  ({new FileInfo(file).Length} B)");
            }
        }

        if (Directory.Exists(Root))
        {
            Walk(Root, "  ");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
