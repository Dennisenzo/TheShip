using System.Text.Json;

namespace TheShip.AI;

/// <summary>
/// Reading the model's structured answers. The grammars make them JSON, but the model may still wrap one in
/// template tags or a thought channel, so the first <c>{</c> to the last <c>}</c> is what's parsed.
/// </summary>
public static class LlmJson
{
    /// <summary>The JSON object in <paramref name="raw"/>, or null if there isn't a well-formed one.</summary>
    public static JsonElement? TryParse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string property of an object, or null if it's missing or not a string.</summary>
    public static string? Str(JsonElement? e, string name) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    /// <summary>A string property, trimmed, or "" if it's missing or not a string.</summary>
    public static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()?.Trim() ?? "" : "";

    /// <summary>A non-negative integer property, or 0 if it's missing or not a number.</summary>
    public static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) ? Math.Max(0, n) : 0;
}
