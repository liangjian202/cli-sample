using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentToolsDemo;

internal static class Program
{
    // 这份「计划」就是模型在真实场景下会返回的一串 FunctionCall。
    // 下面先用它分别驱动两个工具，验证行为一致。
    private static readonly (string Operation, string Path, string? Content, string Why)[] Plan =
    [
        ("create_folder", "notes", null, "建目录"),
        ("create_file", "notes/hello.txt", "hello, 这是工具写入的内容", "写文件（父目录已存在）"),
        ("create_file", "notes/tmp.txt", "临时内容", "再写一个待删文件"),
        ("delete_file", "notes/tmp.txt", null, "删文件"),
        ("delete_folder", "notes", null, "递归删目录")
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions CallJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Sandbox.Reset();

        var codeTool = CodeFileTools.Create();   // L1：托管 API
        var cliTool = CliFileTools.Create();     // L2：固定命令模板 + 子进程
        var shellTool = ShellTool.Create();      // L3：模型自己写命令

        PrintToolCard(codeTool);
        PrintToolCard(cliTool);
        PrintToolCard(shellTool);

        Console.WriteLine("=== 第一轮：同一份调用脚本 -> code_file_ops（.NET API）===\n");
        await RunPlanAsync(codeTool);

        Sandbox.Reset();
        Console.WriteLine("\n=== 第二轮：沙箱重置，同一份脚本 -> cli_file_ops（PowerShell）===\n");
        await RunPlanAsync(cliTool);

        await RunGuardDemoAsync(codeTool, cliTool);
        await RunShellDemoAsync(shellTool);
        await RunAgentIfConfiguredAsync(codeTool, cliTool, shellTool);
    }

    /// <summary>把工具注册给模型时，模型真正「看到」的东西。</summary>
    private static void PrintToolCard(AIFunction tool)
    {
        Console.WriteLine($"[工具] {tool.Name}");
        Console.WriteLine($"  描述: {tool.Description}");
        Console.WriteLine("  参数 schema:");
        var schema = JsonSerializer.Serialize(tool.JsonSchema, JsonOptions);
        foreach (var line in schema.Split('\n'))
        {
            Console.WriteLine("    " + line.TrimEnd());
        }
        Console.WriteLine();
    }

    private static async Task RunPlanAsync(AIFunction tool)
    {
        foreach (var step in Plan)
        {
            // 模拟模型返回的结构化调用（真实场景由 LLM 生成）
            var simulatedCall = JsonSerializer.Serialize(new
            {
                name = tool.Name,
                arguments = new { operation = step.Operation, path = step.Path, content = step.Content }
            }, CallJsonOptions);
            Console.WriteLine($"→ 模型返回: {simulatedCall}");
            Console.WriteLine($"  （意图: {step.Why}）");

            try
            {
                var result = await tool.InvokeAsync(new AIFunctionArguments
                {
                    ["request"] = new FileOpRequest
                    {
                        Operation = step.Operation,
                        Path = step.Path,
                        Content = step.Content
                    }
                });
                Console.WriteLine($"← 工具返回: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"← 工具抛错: {ex.GetBaseException().Message}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("沙箱当前状态:");
        Console.WriteLine(Sandbox.Tree());
    }

    /// <summary>
    /// 护栏演示：模型可能输出越界路径（被提示注入、或单纯理解错）。
    /// 两个工具都会在解析路径时拒绝，CLI 分支甚至不会启动子进程。
    /// </summary>
    private static async Task RunGuardDemoAsync(AIFunction codeTool, AIFunction cliTool)
    {
        Console.WriteLine("\n=== 第三轮：越界路径被运行时拦截 ===\n");

        string[] badPaths = ["../escape.txt", "C:\\Windows\\Temp\\evil.txt", "notes/../../escape.txt"];

        foreach (var tool in new[] { codeTool, cliTool })
        {
            foreach (var badPath in badPaths)
            {
                try
                {
                    var result = await tool.InvokeAsync(new AIFunctionArguments
                    {
                        ["request"] = new FileOpRequest
                        {
                            Operation = "create_file",
                            Path = badPath,
                            Content = "should never be written"
                        }
                    });
                    Console.WriteLine($"{tool.Name} path={badPath} -> 未拦截！{result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{tool.Name} path={badPath} -> 已拦截: {ex.GetBaseException().Message}");
                }
            }
        }
    }

    /// <summary>
    /// L3 对比：命令不是预先写好的，而是「模型自己拼」的。
    /// 前四条是正常任务，后三条是模型（或注入内容）可能生成的越界命令。
    /// </summary>
    private static async Task RunShellDemoAsync(AIFunction shellTool)
    {
        Console.WriteLine("\n=== 第四轮：run_command，命令由模型自己编写 ===\n");
        Sandbox.Reset();

        (string Command, string Kind)[] commands =
        [
            ("New-Item -ItemType Directory -Force -Path reports", "正常：建目录"),
            ("Set-Content -Path reports\\summary.txt -Value 'cli tool wrote this'", "正常：写文件"),
            ("Get-ChildItem -Path reports -Recurse", "正常：看目录内容"),
            ("Remove-Item -Path reports -Recurse -Force", "正常：删目录"),
            ("Remove-Item -Path C:\\Windows\\Temp -Recurse -Force", "越界：绝对路径"),
            ("Get-ChildItem ; Remove-Item -Path reports -Recurse", "越界：命令拼接"),
            ("Set-Content -Path ..\\escape.txt -Value 'x'", "越界：目录穿越")
        ];

        foreach (var (command, kind) in commands)
        {
            var call = JsonSerializer.Serialize(new
            {
                name = shellTool.Name,
                arguments = new { command }
            }, CallJsonOptions);
            Console.WriteLine($"→ 模型返回: {call}");
            Console.WriteLine($"  （{kind}）");

            try
            {
                var result = await shellTool.InvokeAsync(new AIFunctionArguments
                {
                    ["request"] = new ShellRequest { Command = command }
                });
                var firstLines = string.Join(" | ", result!.ToString()!
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Take(3));
                Console.WriteLine($"← 工具返回: {firstLines}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"← 已拦截: {ex.GetBaseException().Message}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("沙箱当前状态:");
        Console.WriteLine(Sandbox.Tree());
    }

    /// <summary>
    /// 真正接模型的部分：配好 OPENAI_API_KEY 就会跑一次完整的 function-calling 循环。
    /// Agent Framework 会自动完成 调模型 -> 执行工具 -> 回填结果 -> 再调模型 的循环。
    /// </summary>
    private static async Task RunAgentIfConfiguredAsync(AIFunction codeTool, AIFunction cliTool, AIFunction shellTool)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("\n=== 接真实模型 ===");
            Console.WriteLine("未检测到 OPENAI_API_KEY，跳过。设置后重新运行即可看到完整的 agent 循环：");
            Console.WriteLine("  export OPENAI_API_KEY=sk-...");
            Console.WriteLine("  export OPENAI_MODEL=gpt-4o-mini");
            return;
        }

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        var options = new OpenAI.OpenAIClientOptions();
        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.Endpoint = new Uri(baseUrl);
        }

        var client = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);

        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        AIAgent agent = chatClient.AsAIAgent(
            name: "FileOpsAgent",
            instructions: """
                你负责在沙箱目录内管理文件，有三个工具可用：
                - run_command：你自己编写 PowerShell 命令执行，最灵活，优先使用它；
                - cli_file_ops：命令模板固定，只填 operation/path/content；
                - code_file_ops：纯 .NET 实现，不经过命令行。
                所有路径都必须是沙箱内的相对路径。
                """,
            tools: [shellTool, codeTool, cliTool]);

        Sandbox.Reset();
        Console.WriteLine("\n=== 接真实模型 ===");

        const string prompt = "在 reports 目录里写一个 report.md，内容是一句话的工作总结；然后再建一个空的 drafts 目录，最后把 drafts 删掉。";
        Console.WriteLine($"用户: {prompt}\n");

        AgentResponse response = await agent.RunAsync(prompt);
        Console.WriteLine($"Agent: {response.Text}");
        Console.WriteLine("\n沙箱最终状态:");
        Console.WriteLine(Sandbox.Tree());
    }
}
