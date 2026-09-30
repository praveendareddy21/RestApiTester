using Microsoft.Extensions.Options;
using RestApiTester.Models;
using System.Text.Json;

namespace RestApiTester.Services;

public class OAuth2TokenService
{
    private readonly OAuth2Settings _settings;
    private readonly IHttpClientFactory _http;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string?  _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public OAuth2TokenService(IOptions<OAuth2Settings> settings, IHttpClientFactory http)
    {
        _settings = settings.Value;
        _http     = http;
    }

    // True when all three required fields are populated in appsettings.json
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.TokenUrl) &&
        !string.IsNullOrWhiteSpace(_settings.ClientId) &&
        !string.IsNullOrWhiteSpace(_settings.ClientSecret);

    // Returns a valid access token, using the in-memory cache until 30 s before expiry.
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        // Fast path: cached token still valid
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring the lock
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
                return _cachedToken;

            var client = _http.CreateClient();

            var body = new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
            };
            if (!string.IsNullOrWhiteSpace(_settings.Scope))
                body["scope"] = _settings.Scope;

            var response = await client.PostAsync(
                _settings.TokenUrl,
                new FormUrlEncodedContent(body),
                ct);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var token = root.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;

            if (token != null)
            {
                var expiresIn = root.TryGetProperty("expires_in", out var expProp)
                    && expProp.TryGetInt32(out var exp) ? exp : 3600;

                _cachedToken = token;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30); // 30 s safety buffer
            }

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Call before GetAccessTokenAsync to force a new fetch regardless of cache state.
    public void ClearCache()
    {
        _cachedToken = null;
        _tokenExpiry = DateTime.MinValue;
    }
}
