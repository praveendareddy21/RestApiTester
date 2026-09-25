using System.Text.Json;
using System.Xml.Linq;

namespace RestApiTester.Services;

public class SoapToJsonConverter
{
    /// <summary>
    /// Locates the SOAP Body, strips all XML namespaces, and serialises the inner
    /// element tree to indented JSON.  Repeating sibling tags become JSON arrays.
    /// </summary>
    public ConversionResult Convert(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new ConversionResult { Error = "Input is empty." };

        try
        {
            var doc = XDocument.Parse(xml.Trim());

            var body = doc.Descendants()
                          .FirstOrDefault(e => e.Name.LocalName == "Body");

            if (body == null)
                return new ConversionResult { Error = "No <Body> element found. Make sure the input is a valid SOAP envelope." };

            var parsed = ParseElement(body);
            var json   = JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });

            return new ConversionResult { Json = json };
        }
        catch (Exception ex)
        {
            return new ConversionResult { Error = $"Parse error: {ex.Message}" };
        }
    }

    // Recursively turns an XElement into a string (leaf) or Dictionary (node).
    // Namespace prefixes are stripped; repeating tag names become List<object>.
    private static object ParseElement(XElement element)
    {
        if (!element.HasElements)
            return element.Value;

        var dict = new Dictionary<string, object>();

        foreach (var child in element.Elements())
        {
            var key    = child.Name.LocalName;
            var parsed = ParseElement(child);

            if (dict.TryGetValue(key, out var existing))
            {
                if (existing is List<object> list)
                    list.Add(parsed);
                else
                    dict[key] = new List<object> { existing, parsed };
            }
            else
            {
                dict[key] = parsed;
            }
        }

        return dict;
    }
}

public class ConversionResult
{
    public string? Json  { get; set; }
    public string? Error { get; set; }
    public bool    Success => Error == null;
}
