namespace RestApiTester.Models;

public class ApiParameter
{
    public string Name { get; set; } = "";
    public string In { get; set; } = "query"; // query, path, header, cookie
    public bool Required { get; set; }
    public string Type { get; set; } = "string";
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
    public string Value { get; set; } = "";
}
