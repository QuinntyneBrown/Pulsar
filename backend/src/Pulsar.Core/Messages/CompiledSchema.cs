using System.Text.Json;

namespace Pulsar.Core.Messages;

/// <summary>
/// A JSON Schema parsed once and kept ready for repeated advisory validation. The
/// schema element is detached (cloned) so it outlives the <see cref="JsonDocument"/>
/// it was parsed from.
/// </summary>
public sealed class CompiledSchema
{
    private CompiledSchema(JsonElement root, string rawJson)
    {
        Root = root;
        RawJson = rawJson;
    }

    /// <summary>The schema as a detached JSON element.</summary>
    internal JsonElement Root { get; }

    /// <summary>The original schema text (handy for sending to a client).</summary>
    public string RawJson { get; }

    /// <summary>Parses a schema document. Throws <see cref="JsonException"/> on malformed JSON.</summary>
    public static CompiledSchema Parse(string schemaJson)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        return new CompiledSchema(doc.RootElement.Clone(), schemaJson);
    }

    /// <summary>
    /// Resolves a leading-<c>#</c> JSON pointer fragment (e.g. <c>#/$defs/Heartbeat</c>)
    /// against a freshly parsed document and returns the targeted sub-schema, or the
    /// whole document when there is no fragment.
    /// </summary>
    public static CompiledSchema ParseWithPointer(string schemaJson, string? jsonPointer)
    {
        if (string.IsNullOrWhiteSpace(jsonPointer) || jsonPointer is "#" or "#/")
            return Parse(schemaJson);

        using var doc = JsonDocument.Parse(schemaJson);
        var node = doc.RootElement;
        var pointer = jsonPointer.TrimStart('#').Trim('/');
        foreach (var rawToken in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Replace("~1", "/").Replace("~0", "~");
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(token, out var next))
                throw new JsonException($"JSON pointer '{jsonPointer}' does not resolve in the schema.");
            node = next;
        }

        var detached = node.Clone();
        return new CompiledSchema(detached, detached.GetRawText());
    }
}
