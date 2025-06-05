using ModelContextProtocol.Client;
using System.Text.Json;

namespace MinimalMCP;

public static class McpClientToolExtension
{
    /// <summary>
    /// 格式化工具信息供LLM使用
    /// </summary>
    public static string Format(this McpClientTool tool)
    {
        IReadOnlyList<string> parameterDescriptions = FormatParameterDescription(tool.JsonSchema);

        return $@"
工具: {tool.Name}
描述: {tool.Description}
参数:
{string.Join(Environment.NewLine, parameterDescriptions)}";
    }

    private static List<string> FormatParameterDescription(JsonElement schema)
    {
        List<string> metadata = [];

        if (!schema.TryGetProperty("properties", out JsonElement properties))
        {
            return [];
        }

        string parameterFormat = "- {0}: {1} {2}";

        HashSet<string>? requiredParameterNames = GetRequiredParameterNames(schema);

        foreach (var param in properties.EnumerateObject())
        {
            string? paramDescription = param.Value.TryGetProperty("description", out JsonElement description) ? description.GetString() : null;
            bool isRequired = requiredParameterNames?.Contains(param.Name) ?? false;

            metadata.Add(string.Format(parameterFormat, param.Name, paramDescription, isRequired ? "(Required)" : ""));
        }

        return metadata;
    }

    /// <summary>
    /// 获取必填的参数的名称
    /// </summary>
    /// <param name="schema">参数的 schema</param>
    /// <returns></returns>
    private static HashSet<string>? GetRequiredParameterNames(JsonElement schema)
    {
        HashSet<string>? requiredParameterNames = null;

        if (schema.TryGetProperty("required", out JsonElement requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in requiredElement.EnumerateArray())
            {
                requiredParameterNames ??= [];
                requiredParameterNames.Add(node.GetString()!);
            }
        }

        return requiredParameterNames;
    }
}