using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using RestApiTester.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestApiTester.Services;

public class OpenApiParserService
{
    public ParseResult Parse(string content)
    {
        try
        {
            var reader = new OpenApiStringReader();
            var document = reader.Read(content, out var diagnostic);

            if (document == null)
                return new ParseResult { Error = "Failed to parse OpenAPI document." };

            var serverUrls = document.Servers?.Select(s => s.Url).Where(u => !string.IsNullOrWhiteSpace(u)).ToList()
                             ?? new List<string>();
            if (!serverUrls.Any()) serverUrls.Add("/");

            var proxiesMap = new Dictionary<string, ApiProxy>(StringComparer.OrdinalIgnoreCase);

            // Seed tags from the global tags list (preserves ordering + descriptions)
            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    proxiesMap[tag.Name] = new ApiProxy
                    {
                        Name = tag.Name,
                        Description = tag.Description ?? ""
                    };
                }
            }

            foreach (var pathItem in document.Paths)
            {
                var pathKey = pathItem.Key;
                var pathLevelParams = pathItem.Value.Parameters ?? new List<OpenApiParameter>();

                foreach (var operation in pathItem.Value.Operations)
                {
                    var httpMethod = operation.Key.ToString().ToUpperInvariant();
                    var op = operation.Value;

                    var tags = op.Tags?.Select(t => t.Name).ToList() ?? new List<string>();
                    if (!tags.Any()) tags.Add("Default");

                    foreach (var tagName in tags)
                    {
                        if (!proxiesMap.ContainsKey(tagName))
                            proxiesMap[tagName] = new ApiProxy { Name = tagName };

                        var endpoint = BuildEndpoint(pathKey, httpMethod, op, pathLevelParams, tagName, document);
                        proxiesMap[tagName].Endpoints.Add(endpoint);
                    }
                }
            }

            return new ParseResult
            {
                Proxies = proxiesMap.Values.ToList(),
                ServerUrls = serverUrls
            };
        }
        catch (Exception ex)
        {
            return new ParseResult { Error = $"Parse error: {ex.Message}" };
        }
    }

    private ApiEndpoint BuildEndpoint(
        string path, string method, OpenApiOperation op,
        IList<OpenApiParameter> pathLevelParams, string tag,
        OpenApiDocument document)
    {
        var endpoint = new ApiEndpoint
        {
            Path = path,
            Method = method,
            OperationId = op.OperationId ?? $"{method} {path}",
            Summary = op.Summary ?? "",
            Description = op.Description ?? "",
            Tag = tag
        };

        // Merge path-level params with operation-level (operation wins on duplicates)
        var opParams = op.Parameters ?? new List<OpenApiParameter>();
        var allParams = pathLevelParams
            .Where(p => !opParams.Any(op2 => op2.Name == p.Name && op2.In == p.In))
            .Concat(opParams)
            .ToList();

        foreach (var param in allParams)
        {
            endpoint.Parameters.Add(new ApiParameter
            {
                Name = param.Name ?? "",
                In = param.In switch
                    {
                        ParameterLocation.Path   => "path",
                        ParameterLocation.Query  => "query",
                        ParameterLocation.Header => "header",
                        ParameterLocation.Cookie => "cookie",
                        _                        => "query"
                    },
                Required = param.Required,
                Type = param.Schema?.Type ?? "string",
                Description = param.Description,
                DefaultValue = GetSchemaDefault(param.Schema),
                Value = GetSchemaDefault(param.Schema) ?? ""
            });
        }

        // Request body
        if (op.RequestBody != null)
        {
            var jsonContent = op.RequestBody.Content
                .FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

            var chosen = jsonContent.Value != null ? jsonContent : op.RequestBody.Content.FirstOrDefault();
            if (chosen.Value != null)
            {
                endpoint.ContentType = chosen.Key;
                endpoint.RequestBodyExample = BuildExample(chosen.Value.Schema, document, 0);
            }
        }

        // Responses
        foreach (var resp in op.Responses)
            endpoint.ResponseDescriptions[resp.Key] = resp.Value.Description ?? "";

        return endpoint;
    }

    private string? GetSchemaDefault(OpenApiSchema? schema)
    {
        if (schema?.Default == null) return null;
        try
        {
            return schema.Default switch
            {
                OpenApiString s  => s.Value,
                OpenApiInteger i => i.Value.ToString(),
                OpenApiLong l    => l.Value.ToString(),
                OpenApiFloat f   => f.Value.ToString(),
                OpenApiDouble d  => d.Value.ToString(),
                OpenApiBoolean b => b.Value.ToString().ToLower(),
                _                => null
            };
        }
        catch { return null; }
    }

    private string BuildExample(OpenApiSchema? schema, OpenApiDocument document, int depth)
    {
        if (schema == null) return "{}";
        try
        {
            var node = BuildNode(schema, document, depth);
            return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return "{}";
        }
    }

    private JsonNode? BuildNode(OpenApiSchema? schema, OpenApiDocument doc, int depth)
    {
        if (schema == null || depth > 5) return null;

        // allOf — merge all sub-schemas into one object
        if (schema.AllOf?.Count > 0)
        {
            var merged = new JsonObject();
            foreach (var sub in schema.AllOf)
            {
                if (BuildNode(sub, doc, depth) is JsonObject subObj)
                {
                    var keys = subObj.Select(kv => kv.Key).ToList();
                    foreach (var key in keys)
                    {
                        var val = subObj[key]?.DeepClone();
                        merged.TryAdd(key, val);
                    }
                }
            }
            return merged;
        }

        if (schema.AnyOf?.Count > 0) return BuildNode(schema.AnyOf[0], doc, depth);
        if (schema.OneOf?.Count > 0) return BuildNode(schema.OneOf[0], doc, depth);

        var type = schema.Type;
        if (string.IsNullOrEmpty(type))
        {
            if (schema.Properties?.Any() == true) type = "object";
            else if (schema.Items != null) type = "array";
            else type = "string";
        }

        return type switch
        {
            "object" => BuildObjectNode(schema, doc, depth),
            "array" => BuildArrayNode(schema, doc, depth),
            "integer" => JsonValue.Create(0),
            "number" => JsonValue.Create(0.0),
            "boolean" => JsonValue.Create(false),
            "string" => JsonValue.Create(StringExample(schema)),
            _ => JsonValue.Create("string")
        };
    }

    private JsonObject BuildObjectNode(OpenApiSchema schema, OpenApiDocument doc, int depth)
    {
        var obj = new JsonObject();
        if (schema.Properties == null) return obj;
        foreach (var (key, value) in schema.Properties)
            obj[key] = BuildNode(value, doc, depth + 1);
        return obj;
    }

    private JsonArray BuildArrayNode(OpenApiSchema schema, OpenApiDocument doc, int depth)
    {
        var arr = new JsonArray();
        var item = BuildNode(schema.Items, doc, depth + 1);
        if (item != null) arr.Add(item);
        return arr;
    }

    private static string StringExample(OpenApiSchema schema) => schema.Format switch
    {
        "date-time" => "2024-01-01T00:00:00Z",
        "date" => "2024-01-01",
        "uuid" or "guid" => "00000000-0000-0000-0000-000000000000",
        "email" => "user@example.com",
        "uri" or "url" => "https://example.com",
        "password" => "password",
        "byte" => "dGVzdA==",
        _ => schema.Enum?.Any() == true
            ? ((schema.Enum.First() as OpenApiString)?.Value ?? "string")
            : "string"
    };
}

public class ParseResult
{
    public List<ApiProxy> Proxies { get; set; } = new();
    public List<string> ServerUrls { get; set; } = new();
    public string? Error { get; set; }
    public bool Success => Error == null;
}
