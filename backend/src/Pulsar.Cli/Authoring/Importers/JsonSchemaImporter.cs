using System.Text.Json;
using Pulsar.Core;

namespace Pulsar.Cli.Authoring.Importers;

/// <summary>
/// Imports a single JSON Schema file as a one-message plugin. The schema is already in
/// Pulsar's native shape, so this is the simplest importer and needs no third-party
/// dependency: the file becomes the message's schema verbatim.
/// </summary>
public sealed class JsonSchemaImporter : ISpecImporter
{
    public string Format => "jsonschema";
    public string Description => "A single JSON Schema document (.json) → one message.";

    public PluginDraft Import(Stream spec, ImportOptions options)
    {
        string text;
        using (var reader = new StreamReader(spec))
            text = reader.ReadToEnd();

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(text);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new PluginLoadException($"Input is not valid JSON: {ex.Message}", ex);
        }

        var title = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;

        var key = string.IsNullOrWhiteSpace(title) ? "Message" : title!.Trim();
        var category = Naming.Categorize(key);
        var message = new DraftMessage(
            Key: key,
            DisplayName: key,
            Category: category,
            DefaultChannel: Naming.DefaultChannel(key, category),
            SchemaJson: text);

        return new PluginDraft(options.PluginName, options.Adapter, new[] { message });
    }
}
