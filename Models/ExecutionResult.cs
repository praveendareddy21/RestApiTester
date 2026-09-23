namespace RestApiTester.Models;

public class ExecutionResult
{
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; } = "";
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    public string ResponseBody { get; set; } = "";
    public string RequestUrl { get; set; } = "";
    public string RequestMethod { get; set; } = "";
    public string? RequestBody { get; set; }
    public Dictionary<string, string> RequestHeadersSent { get; set; } = new();
    public long ElapsedMilliseconds { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusClass => StatusCode switch
    {
        >= 200 and < 300 => "status-2xx",
        >= 300 and < 400 => "status-3xx",
        >= 400 and < 500 => "status-4xx",
        >= 500 => "status-5xx",
        _ => "status-unknown"
    };
}
