using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestApiTester.Models;

public class JsonTreeNode
{
    public string?            Key         { get; set; }
    public string?            ValueStr    { get; set; }
    public string             Kind        { get; set; } = "null";
    public List<JsonTreeNode> Children    { get; set; } = new();
    public bool               IsCollapsed { get; set; }
    public bool               IsLast      { get; set; } = true;

    // Parse a JSON string into a tree. Returns null if the text is not valid JSON.
    public static JsonTreeNode? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try   { return Build(JsonNode.Parse(json), null, true, 0); }
        catch { return null; }
    }

    private static JsonTreeNode Build(JsonNode? node, string? key, bool isLast, int depth)
    {
        var t = new JsonTreeNode { Key = key, IsLast = isLast, IsCollapsed = depth > 1 };

        if (node is JsonObject obj)
        {
            t.Kind = "object";
            var pairs = obj.ToList();
            for (int i = 0; i < pairs.Count; i++)
                t.Children.Add(Build(pairs[i].Value, pairs[i].Key, i == pairs.Count - 1, depth + 1));
        }
        else if (node is JsonArray arr)
        {
            t.Kind = "array";
            for (int i = 0; i < arr.Count; i++)
                t.Children.Add(Build(arr[i], null, i == arr.Count - 1, depth + 1));
        }
        else
        {
            var raw = JsonSerializer.Serialize(node);
            t.Kind = raw == "null"        ? "null"   :
                     raw is "true"        or
                     "false"             ? "bool"   :
                     raw.StartsWith('"') ? "string" : "number";
            t.ValueStr = raw;
        }

        return t;
    }
}
