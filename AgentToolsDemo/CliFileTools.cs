using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace AgentToolsDemo;

/// <summary>
/// 工具 B：调用命令行实现同样的四个功能。
/// 关键在于：命令模板是写死的常量，模型只能改「数据」（路径、内容），
/// 而且数据通过环境变量传入，不拼进命令行字符串 —— 从根上避免命令注入。
/// </summary>
public static class CliFileTools
{
    public static AIFunction Create() => AIFunctionFactory.Create(
        (FileOpRequest request) => Execute(request),
        name: "cli_file_ops",
        description: FileOpContract.ToolDescription + " 本实现通过调用 PowerShell 命令行完成。");

    public static string Execute(FileOpRequest request)
    {
        var kind = FileOpContract.Parse(request.Operation);
        var full = Sandbox.Resolve(request.Path);      // 先过一遍沙箱校验
        var relative = Sandbox.Relative(full);

        // 每个 operation 对应一段固定的 PowerShell 脚本
        var script = kind switch
        {
            FileOpKind.CreateFile =>
                """
                $parent = Split-Path -Parent $env:OP_PATH
                if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
                $enc = New-Object System.Text.UTF8Encoding($false)
                [System.IO.File]::WriteAllText($env:OP_PATH, $env:OP_CONTENT, $enc)
                Write-Output 'created'
                """,
            FileOpKind.DeleteFile =>
                """
                if (Test-Path -LiteralPath $env:OP_PATH -PathType Leaf) {
                    Remove-Item -LiteralPath $env:OP_PATH -Force
                    Write-Output 'deleted'
                } else { Write-Output 'missing' }
                """,
            FileOpKind.CreateFolder =>
                """
                New-Item -ItemType Directory -Force -Path $env:OP_PATH | Out-Null
                Write-Output 'created'
                """,
            FileOpKind.DeleteFolder =>
                """
                if (Test-Path -LiteralPath $env:OP_PATH -PathType Container) {
                    Remove-Item -LiteralPath $env:OP_PATH -Recurse -Force
                    Write-Output 'deleted'
                } else { Write-Output 'missing' }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        var env = new Dictionary<string, string>
        {
            ["OP_PATH"] = full,
            ["OP_CONTENT"] = request.Content ?? ""
        };

        var (exitCode, stdout, stderr) = RunPowerShell(script, env);
        var output = stdout.Trim();

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"命令行执行失败（exit={exitCode}）：{stderr.Trim()}");
        }

        return kind switch
        {
            FileOpKind.CreateFile => $"ok: 已创建文件 {relative}（{new FileInfo(full).Length} B）",
            FileOpKind.DeleteFile => output == "missing" ? $"no-op: 文件不存在 {relative}" : $"ok: 已删除文件 {relative}",
            FileOpKind.CreateFolder => $"ok: 已创建文件夹 {relative}",
            FileOpKind.DeleteFolder => output == "missing" ? $"no-op: 文件夹不存在 {relative}" : $"ok: 已删除文件夹 {relative}",
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    /// <summary>启动子进程执行一段固定脚本；数据和脚本分离，路径不参与命令拼接。</summary>
    private static (int ExitCode, string StdOut, string StdErr) RunPowerShell(
        string script,
        IReadOnlyDictionary<string, string> environment)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = Sandbox.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);          // 脚本是常量，不是模型输出

        foreach (var (key, value) in environment)
        {
            psi.Environment[key] = value;       // 模型给的数据只走这里
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 powershell.exe");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(15_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
            return (-1, stdout, "命令行超时（15s），进程已终止");
        }
        return (process.ExitCode, stdout, stderr);
    }
}
