namespace RestApiTester.Models;

public class ApiEndpoint
{
    public string Path { get; set; } = "";
    public string Method { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tag { get; set; } = "";
    public List<ApiParameter> Parameters { get; set; } = new();
    public string? RequestBodyExample { get; set; }
    public string? ContentType { get; set; }
    public bool HasRequestBody => !string.IsNullOrEmpty(RequestBodyExample) || ContentType != null;
    public Dictionary<string, string> ResponseDescriptions { get; set; } = new();

    public string DisplayLabel => string.IsNullOrEmpty(Summary) ? Path : Summary;
}
