namespace MinimalMCP;

public class McpServerConfigRoot
{
    public Dictionary<string, McpServerConfig> McpServers { get; set; }
}

public class McpServerConfig
{
    public string Command { get; set; }
    public List<string> Args { get; set; }
}