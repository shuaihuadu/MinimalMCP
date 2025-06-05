using Microsoft.Extensions.Configuration;

namespace MinimalMCP;

class Program
{
    static async Task Main(string[] args)
    {
        // 获取MCP Server配置
        IConfigurationRoot configRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.dev.json")
            .Build();

        McpServerConfigRoot mcpConfig = new();
        configRoot.Bind(mcpConfig);

        ChatSession chatSession = new(configRoot);

        await chatSession.StartAsync();
    }
}