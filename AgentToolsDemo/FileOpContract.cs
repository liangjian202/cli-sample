using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentToolsDemo;

/// <summary>
/// 两个工具共用的入参形状 —— 模型看到的 schema 完全一致，
/// 差别只在运行时怎么执行（托管 API vs 子进程）。
/// </summary>
public sealed class FileOpRequest
{
    [Description("操作类型，取值之一：create_file、delete_file、create_folder、delete_folder")]
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [Description("相对沙箱根目录的路径，例如 notes/a.txt 或 notes。不要用绝对路径，不要用 ..")]
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [Description("仅 create_file 使用：要写入的文本内容；其他操作忽略此字段")]
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public enum FileOpKind
{
    CreateFile,
    DeleteFile,
    CreateFolder,
    DeleteFolder
}

public static class FileOpContract
{
    public const string ToolDescription =
        "对沙箱目录执行文件/文件夹操作。支持四种 operation：" +
        "create_file（创建文件，需要 content）、delete_file（删除文件）、" +
        "create_folder（创建文件夹，含父目录）、delete_folder（递归删除文件夹）。";

    /// <summary>把模型给的字符串归一化，非法取值直接拒绝并给出可选项。</summary>
    public static FileOpKind Parse(string? operation) =>
        operation?.Trim().ToLowerInvariant() switch
        {
            "create_file" => FileOpKind.CreateFile,
            "delete_file" => FileOpKind.DeleteFile,
            "create_folder" => FileOpKind.CreateFolder,
            "delete_folder" => FileOpKind.DeleteFolder,
            null or "" => throw new ArgumentException("operation 不能为空"),
            _ => throw new ArgumentException(
                $"不支持的 operation：{operation}。可选：create_file、delete_file、create_folder、delete_folder")
        };
}
