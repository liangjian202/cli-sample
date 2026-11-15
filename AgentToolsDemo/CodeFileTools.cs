using Microsoft.Extensions.AI;

namespace AgentToolsDemo;

/// <summary>
/// 工具 A：用 .NET 托管 API 实现（System.IO）。
/// 模型看到的是一个 AIFunction，看不到下面的实现。
/// </summary>
public static class CodeFileTools
{
    public static AIFunction Create() => AIFunctionFactory.Create(
        (FileOpRequest request) => Execute(request),
        name: "code_file_ops",
        description: FileOpContract.ToolDescription + " 本实现使用 .NET 托管 API。");

    public static string Execute(FileOpRequest request)
    {
        var kind = FileOpContract.Parse(request.Operation);
        var full = Sandbox.Resolve(request.Path);
        var relative = Sandbox.Relative(full);

        switch (kind)
        {
            case FileOpKind.CreateFile:
            {
                var parent = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }
                File.WriteAllText(full, request.Content ?? "");
                return $"ok: 已创建文件 {relative}（{new FileInfo(full).Length} B）";
            }

            case FileOpKind.DeleteFile:
                if (!File.Exists(full))
                {
                    return $"no-op: 文件不存在 {relative}";
                }
                File.Delete(full);
                return $"ok: 已删除文件 {relative}";

            case FileOpKind.CreateFolder:
                Directory.CreateDirectory(full);
                return $"ok: 已创建文件夹 {relative}";

            case FileOpKind.DeleteFolder:
                if (!Directory.Exists(full))
                {
                    return $"no-op: 文件夹不存在 {relative}";
                }
                Directory.Delete(full, recursive: true);
                return $"ok: 已删除文件夹 {relative}";

            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
    }
}
