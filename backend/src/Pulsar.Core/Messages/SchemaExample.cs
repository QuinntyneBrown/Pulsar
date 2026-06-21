using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pulsar.Core.Messages;

/// <summary>
/// Derives a seed example payload from a JSON Schema — the single source of truth for
/// "what JSON should the editor open with" when a message has no explicit example file.
/// Used both by <see cref="Pulsar.Core.Plugins.ManifestPluginLoader"/> (to seed a catalog
/// entry) and by the CLI's <c>gen-example</c> command, so the two can never diverge.
/// </summary>
public static class SchemaExample
{
    /// <summary>
    /// Returns a pretty-printed example for <paramref name="schema"/>, preferring (in
    /// order) the schema's first <c>examples</c> entry, its object <c>default</c>, a
    /// skeleton built from each property's <c>default</c>/<c>type</c>, and finally
    /// <c>"{}"</c> when there is nothing to go on. Never throws.
    /// </summary>
    public static string FromSchema(CompiledSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return FromRoot(schema.Root);
    }

    internal static string FromRoot(JsonElement root)
    {
        // 1. The schema's first `examples` entry.
        if (root.TryGetProperty("examples", out var examples)
            && examples.ValueKind == JsonValueKind.Array && examples.GetArrayLength() > 0)
            return Prettify(examples[0].GetRawText());

        // 2. The schema's `default`, when it is an object.
        if (root.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.Object)
            return Prettify(def.GetRawText());

        // 3. A skeleton built from each property's default/type.
        var skeleton = BuildSkeleton(root);
        if (skeleton is not null)
            return Prettify(skeleton.ToJsonString());

        // 4. Nothing to go on.
        return "{}";
    }

    private static JsonObject? BuildSkeleton(JsonElement schema)
    {
        if (!schema.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;

        var obj = new JsonObject();
        foreach (var prop in props.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("default", out var def))
            {
                obj[prop.Name] = JsonNode.Parse(def.GetRawText());
                continue;
            }

            var type = prop.Value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
            obj[prop.Name] = type switch
            {
                "string" => "",
                "integer" or "number" => (JsonNode)0,
                "boolean" => false,
                "array" => new JsonArray(),
                "object" => new JsonObject(),
                _ => null,
            };
        }
        return obj;
    }

    private static string Prettify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, MessageTemplateService.JsonOptions);
        }
        catch (JsonException)
        {
            return json; // leave it as-is; the caller will still surface it and validation flags it.
        }
    }
}
