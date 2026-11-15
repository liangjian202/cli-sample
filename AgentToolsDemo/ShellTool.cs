using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace AgentToolsDemo;

public sealed class ShellRequest
{
    [Description("要执行的 PowerShell 单条命令，例如 New-Item -ItemType Directory -Force -Path reports")]
    public string Command { get; set; } = "";

    [Description("可选：用一句话说明为什么要执行这条命令，便于审计日志")]
    public string? Reason { get; set; }
}

/// <summary>
/// L3 形态：通用命令执行工具。命令由模型自己编写，运行时只做「审核 + 执行」。
/// 这是 Claude Code 那类 agent 的做法，灵活度最高，风险也最高。
/// </summary>
public static class ShellTool
{
    public static AIFunction Create() => AIFunctionFactory.Create(
        (ShellRequest request) => Execute(request),
        name: "run_command",
        description:
            "在沙箱目录内执行一条 PowerShell 命令并返回 stdout/stderr/exit code。" +
            "命令由你编写，例如 New-Item、Set-Content、Get-ChildItem、Remove-Item。" +
            "只允许单条命令：不支持 ; | && $( ) 等组合与重定向；路径必须在沙箱内。");

    // 组合算子一律禁止：单条命令可审核，拼接后就不可审核了
    private static readonly string[] ForbiddenOperators = [";", "&&", "||", "|", "`", "$(", ">", "<"];

    // 明显越界的危险指令模式
    private static readonly Regex DangerousPattern = new(
        @"(?i)(invoke-expression|\biex\b|start-process|downloadstring|invoke-webrequest|\biwr\b|curl\s|wget\s|reg\s+add|net\s+user|shutdown|format\s+[a-z]:)",
        RegexOptions.Compiled);

    private static readonly Regex PathLikeToken = new(
        @"(?<![\w-])([A-Za-z]:[\\/][^\s'""]*|\\\\[^\s'""]+|\.\.[\\/][^\s'""]*|\.\.[\\/]?|(?<![\w.-])\.{1,2}(?=[\\/]))",
        RegexOptions.Compiled);

    public static string Execute(ShellRequest request)
    {
        var command = request.Command?.Trim() ?? "";
        if (command.Length == 0)
        {
            throw new ArgumentException("command 不能为空");
        }

        GuardCommand(command);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = Sandbox.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        // 这一次，脚本来模型 —— 所以前面的 Guard 必须挡住。
        // 前置一段固定语句，把控制台输出编码切成 UTF-8，否则中文会是乱码（PS 5.1 默认 OEM 代码页）。
        psi.ArgumentList.Add("[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + command);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 powershell.exe");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(20_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
            throw new TimeoutException("命令执行超时（20s），进程已终止");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"exit={process.ExitCode}");
        sb.AppendLine("stdout:");
        sb.AppendLine(Truncate(stdout.Trim(), 4000));
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            sb.AppendLine("stderr:");
            sb.AppendLine(Truncate(stderr.Trim(), 2000));
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>运行时审核：模型写的命令必须能过这一关。</summary>
    private static void GuardCommand(string command)
    {
        foreach (var op in ForbiddenOperators)
        {
            if (command.Contains(op, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"命令包含不允许的算子「{op}」，只接受单条命令");
            }
        }

        var danger = DangerousPattern.Match(command);
        if (danger.Success)
        {
            throw new InvalidOperationException($"命令包含高危指令「{danger.Value}」，已拒绝");
        }

        // 任何看起来像路径的片段，都必须落在沙箱内
        foreach (Match match in PathLikeToken.Matches(command))
        {
            var token = match.Value.Trim('"', '\'');
            try
            {
                Sandbox.Resolve(token);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"命令中的路径「{token}」不合法：{ex.Message}");
            }
        }

        // 禁止把沙箱根目录整个删掉
        if (command.Contains("Remove-Item", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(command, @"-Path\s+(""\.?""|'\.?'|\.)\s", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException("拒绝对沙箱根目录执行 Remove-Item");
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + $"\n...（已截断，共 {text.Length} 字符）";
}
