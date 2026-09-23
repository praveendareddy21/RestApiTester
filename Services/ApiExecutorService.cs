using RestApiTester.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace RestApiTester.Services;

public class ApiExecutorService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiExecutorService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        string baseUrl,
        ApiEndpoint endpoint,
        string requestBody,
        List<HeaderEntry> customHeaders,
        CancellationToken cancellationToken = default)
    {
        var result = new ExecutionResult { RequestMethod = endpoint.Method };
        var sw = Stopwatch.StartNew();

        try
        {
            var url = BuildUrl(baseUrl, endpoint);
            result.RequestUrl = url;

            var client = _httpClientFactory.CreateClient("ApiTester");
            var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), url);

            // Header parameters from OpenAPI spec
            foreach (var param in endpoint.Parameters.Where(p => p.In == "header" && !string.IsNullOrEmpty(p.Value)))
                request.Headers.TryAddWithoutValidation(param.Name, param.Value);

            // Custom headers from UI
            foreach (var h in customHeaders.Where(h => !string.IsNullOrEmpty(h.Key)))
            {
                request.Headers.TryAddWithoutValidation(h.Key, h.Value);
                result.RequestHeadersSent[h.Key] = h.Value;
            }

            // Request body for non-GET methods
            if (!string.IsNullOrWhiteSpace(requestBody) &&
                endpoint.Method is not ("GET" or "HEAD" or "DELETE" or "OPTIONS"))
            {
                var ct = endpoint.ContentType ?? "application/json";
                request.Content = new StringContent(requestBody, Encoding.UTF8, ct);
            }

            result.RequestBody = requestBody;

            var response = await client.SendAsync(request, cancellationToken);
            sw.Stop();

            result.StatusCode = (int)response.StatusCode;
            result.ReasonPhrase = response.ReasonPhrase ?? "";
            result.IsSuccess = response.IsSuccessStatusCode;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            foreach (var h in response.Headers)
                result.ResponseHeaders[h.Key] = string.Join(", ", h.Value);
            foreach (var h in response.Content.Headers)
                result.ResponseHeaders[h.Key] = string.Join(", ", h.Value);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.ResponseBody = TryFormatJson(body);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.IsSuccess = false;
            result.ErrorMessage = "Request cancelled.";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static string BuildUrl(string baseUrl, ApiEndpoint endpoint)
    {
        var url = baseUrl.TrimEnd('/') + endpoint.Path;

        // Replace path parameters
        foreach (var param in endpoint.Parameters.Where(p => p.In == "path" && !string.IsNullOrEmpty(p.Value)))
            url = url.Replace($"{{{param.Name}}}", Uri.EscapeDataString(param.Value));

        // Append query parameters
        var queryParts = endpoint.Parameters
            .Where(p => p.In == "query" && !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value)}")
            .ToList();

        if (queryParts.Any())
            url += "?" + string.Join("&", queryParts);

        return url;
    }

    private static string TryFormatJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        try
        {
            using var doc = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return input;
        }
    }
}

public class HeaderEntry
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
}
