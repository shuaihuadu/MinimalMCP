using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace MinimalMCP;

public class ChatSession(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly List<ChatMessage> _messages = [];

    /// <summary>
    /// 处理LLM响应并执行工具
    /// </summary>
    public static async Task<string> ProcessLlmResponseAsync(string response)
    {
        try
        {
            Dictionary<string, object?>? toolCall = JsonSerializer.Deserialize<Dictionary<string, object?>>(response);

            if (toolCall != null && toolCall.TryGetValue("tool", out object? toolNameValue) && toolCall.TryGetValue("arguments", out object? value))
            {
                string? toolName = toolNameValue!.ToString();

                var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(value!.ToString()!);

                var tools = await ListToolsAsync();

                if (tools.Any(t => t.Name == toolName))
                {
                    try
                    {
                        var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
                        {
                            Name = "Everything",
                            Command = "npx",
                            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
                        });

                        var client = await McpClientFactory.CreateAsync(clientTransport);

                        CallToolResponse callToolResponse = await client.CallToolAsync(toolName, arguments);

                        string toolCallResult = $"工具执行结果: {string.Join(Environment.NewLine, callToolResponse.Content.Select(c => c.Text))}";

                        Console.WriteLine($"[CallToolResult]：使用工具：{toolName}, {toolCallResult}");

                        return toolCallResult;
                    }
                    catch (Exception ex)
                    {
                        string toolCallResult = $"工具执行错误: {ex.Message}";
                        Console.WriteLine($"[CallToolResult] {toolCallResult}");
                    }
                }
                else
                {
                    Console.WriteLine($"[CallToolResult] 未找到工具: {toolName}");
                }
            }

            return response;
        }
        catch (JsonException)
        {
            return response;
        }
    }

    /// <summary>
    /// 启动聊天会话
    /// </summary>
    public async Task StartAsync()
    {
        // 收集所有工具描述
        IList<McpClientTool> tools = await ListToolsAsync(true);

        // 构建系统提示
        StringBuilder toolsDescription = new();

        foreach (var tool in tools)
        {
            toolsDescription.AppendLine(tool.Format());
        }

        string systemMessage = $@"
你是一个有用的助手，可以访问以下工具:

{toolsDescription}

根据用户问题选择合适的工具。如果不需要工具，直接回复。

重要：需要使用工具时，必须仅使用以下JSON格式响应：
{{
    ""tool"": ""工具名称"",
    ""arguments"": {{
        ""参数名"": ""值""
    }}
}}

接收工具响应后：
1. 将原始数据转换为自然对话
2. 保持回复简洁但信息丰富
3. 聚焦最相关信息
4. 使用用户问题的上下文
5. 避免简单重复原始数据
";

        // 添加系统消息
        this._messages.Add(new SystemChatMessage(systemMessage));

        // 主聊天循环
        while (true)
        {
            Console.Write("User: ");
            string input = Console.ReadLine()?.Trim().ToLower() ?? "";

            if (input == "quit" || input == "exit")
            {
                Console.WriteLine("退出...");
                break;
            }

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("请输入内容...");
                continue;
            }

            this._messages.Add(new UserChatMessage(input));

            string llmResponse = await this.GetResponseAsync(this._messages);

            Console.WriteLine($"Assistant: {llmResponse}");

            string response = await ProcessLlmResponseAsync(llmResponse);

            if (response != llmResponse)
            {
                this._messages.Add(new AssistantChatMessage(llmResponse));

                this._messages.Add(new SystemChatMessage(response));

                string finalResponse = await this.GetResponseAsync(this._messages);

                Console.WriteLine($"Final Response: {finalResponse}");

                this._messages.Add(new AssistantChatMessage(finalResponse));
            }
            else
            {
                this._messages.Add(new AssistantChatMessage(llmResponse));
            }
        }
    }

    /// <summary>
    /// 获取MCP服务器的工具列表
    /// 此处使用MCP官方的Everything服务器
    /// <see cref="https://www.npmjs.com/package/@modelcontextprotocol/server-everything"/>
    /// </summary>
    private static async Task<IList<McpClientTool>> ListToolsAsync(bool output = false)
    {
        StdioClientTransportOptions stdioClientTransportOptions = new()
        {
            Name = "Everything",
            Command = "npx",
            Arguments = ["-y", "@modelcontextprotocol/server-everything"],
        };

        StdioClientTransport clientTransport = new(stdioClientTransportOptions);

        IMcpClient client = await McpClientFactory.CreateAsync(clientTransport);

        IList<McpClientTool> mcpClientTools = await client.ListToolsAsync();

        if (mcpClientTools.Any() && output)
        {
            Console.WriteLine($"从MCP服务器[{stdioClientTransportOptions.Name}]获取到的工具信息：");

            foreach (var tool in mcpClientTools)
            {
                Console.WriteLine($"- {tool}");
            }
        }

        return mcpClientTools;
    }

    /// <summary>
    /// 获取LLM响应
    /// </summary>
    private async Task<string> GetResponseAsync(List<ChatMessage> messages)
    {
        string endpoint = this._configuration["AzureOpenAI:Endpoint"]!;
        string apiKey = this._configuration["AzureOpenAI:ApiKey"]!;
        string deploymentName = this._configuration["AzureOpenAI:DeploymentName"]!;

        AzureOpenAIClient azureClient = new(new Uri(endpoint), new AzureKeyCredential(apiKey));

        ChatClient chatClient = azureClient.GetChatClient(deploymentName);

        ChatCompletion completion = await chatClient.CompleteChatAsync(messages);

        return completion.Content[0].Text;
    }
}