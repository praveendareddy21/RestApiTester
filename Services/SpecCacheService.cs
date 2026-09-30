using RestApiTester.Models;

namespace RestApiTester.Services;

// Singleton that caches the parsed OpenAPI spec across all Blazor circuits.
// Without this, every new circuit (reconnect, new tab) re-parses the entire spec.
public class SpecCacheService
{
    private ParseResult? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool HasCache => _cached != null;

    public async Task<ParseResult> GetOrParseAsync(string specPath, OpenApiParserService parser)
    {
        if (_cached != null) return _cached;
        await _lock.WaitAsync();
        try
        {
            if (_cached != null) return _cached;
            var content = await File.ReadAllTextAsync(specPath);
            var result = await Task.Run(() => parser.Parse(content));
            if (result.Success)
                _cached = result;
            return result;
        }
        finally { _lock.Release(); }
    }

    public void Invalidate() => _cached = null;
}
