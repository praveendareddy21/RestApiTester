namespace RestApiTester.Models;

public class ConsoleEntry
{
    public Guid     Id          { get; } = Guid.NewGuid();
    public DateTime Timestamp   { get; } = DateTime.Now;
    public string   Method      { get; init; } = "";
    public string   Url         { get; init; } = "";
    public int      StatusCode  { get; init; }
    public string   StatusClass { get; init; } = "";
    public long     ElapsedMs   { get; init; }
    public string   ErrorMessage { get; init; } = "";

    public Dictionary<string, string> RequestHeaders  { get; init; } = new();
    public string                     RequestBody     { get; init; } = "";
    public Dictionary<string, string> ResponseHeaders { get; init; } = new();
    public string                     ResponseBody    { get; init; } = "";

    // Lazily parsed on first expand — null until then (or when body is not JSON)
    public JsonTreeNode? RequestTree  { get; set; }
    public JsonTreeNode? ResponseTree { get; set; }

    // Mutable UI state
    public bool IsExpanded     { get; set; } = false;
    public bool ShowReqHeaders { get; set; } = false;
    public bool ShowReqBody    { get; set; } = false;
    public bool ShowResHeaders { get; set; } = false;
    public bool ShowResBody    { get; set; } = true;
}
