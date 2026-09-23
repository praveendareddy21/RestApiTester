namespace RestApiTester.Models;

public class ApiProxy
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ApiEndpoint> Endpoints { get; set; } = new();
}
