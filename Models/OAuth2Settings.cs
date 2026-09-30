namespace RestApiTester.Models;

public class OAuth2Settings
{
    public string TokenUrl     { get; set; } = "";
    public string ClientId     { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Scope        { get; set; } = "";
}
