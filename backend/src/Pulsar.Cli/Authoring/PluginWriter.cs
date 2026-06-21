using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pulsar.Core;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;

namespace Pulsar.Cli.Authoring;

/// <summary>The result of writing a draft: where the manifest landed and the catalog it loaded into.</summary>
public sealed record WriteResult(string ManifestPath, LoadedCatalog Catalog);

/// <summary>
/// Writes a <see cref="PluginDraft"/> to a data-only plugin folder
/// (<c>pulsar.plugin.json</c> + <c>schemas/</c> + <c>examples/</c>), then loads it back
/// through the very <see cref="CatalogLoader"/> the running tool uses as a self-check.
/// Shared by <c>new</c> and every importer so the on-disk shape is produced in exactly
/// one place (DRY).
/// </summary>
public sealed class PluginWriter
{
    private readonly CatalogLoader _loader;
    private readonly ILogger<PluginWriter> _logger;

    public PluginWriter(CatalogLoader loader, ILogger<PluginWriter> logger)
    {
        _loader = loader;
        _logger = logger;
    }

    public WriteResult Write(PluginDraft draft, string outputDir, bool overwrite = false)
    {
        if (draft.Messages.Count == 0)
            throw new PluginLoadException("A plugin must declare at least one message.");

        var dir = Path.GetFullPath(outputDir);
        var manifestPath = Path.Combine(dir, "pulsar.plugin.json");
        if (!overwrite && File.Exists(manifestPath))
            throw new PluginLoadException($"A plugin already exists at {manifestPath}. Pass --force to overwrite.");

        var schemasDir = Path.Combine(dir, "schemas");
        var examplesDir = Path.Combine(dir, "examples");
        Directory.CreateDirectory(schemasDir);
        Directory.CreateDirectory(examplesDir);

        var entries = new List<MessageManifestEntry>(draft.Messages.Count);
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var message in draft.Messages)
        {
            if (!usedKeys.Add(message.Key))
                throw new PluginLoadException($"Duplicate message key '{message.Key}' in the draft.");

            CompiledSchema schema;
            try
            {
                schema = CompiledSchema.Parse(message.SchemaJson);
            }
            catch (JsonException ex)
            {
                throw new PluginLoadException($"Schema for '{message.Key}' is not valid JSON: {ex.Message}", ex);
            }

            var slug = Unique(Naming.Slug(message.Key), usedSlugs);
            var schemaRel = $"schemas/{slug}.schema.json";
            var exampleRel = $"examples/{slug}.json";

            File.WriteAllText(Path.Combine(schemasDir, $"{slug}.schema.json"), Prettify(message.SchemaJson));
            var example = string.IsNullOrWhiteSpace(message.ExampleJson)
                ? SchemaExample.FromSchema(schema)
                : Prettify(message.ExampleJson!);
            File.WriteAllText(Path.Combine(examplesDir, $"{slug}.json"), example);

            entries.Add(new MessageManifestEntry(
                message.Key, message.DisplayName, message.Category, message.DefaultChannel, schemaRel, exampleRel));
        }

        var manifest = new PluginManifest(draft.Name, draft.Adapter, entries);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, MessageTemplateService.JsonOptions));

        // Load it back through the production loader — proves what we wrote is loadable.
        var catalog = _loader.Load(manifestPath);
        _logger.LogInformation("Wrote plugin '{Name}' ({Count} message(s)) to {Dir}.",
            catalog.Name, catalog.Messages.Count, dir);
        return new WriteResult(manifestPath, catalog);
    }

    private static string Unique(string slug, HashSet<string> used)
    {
        if (used.Add(slug)) return slug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{slug}-{i}";
            if (used.Add(candidate)) return candidate;
        }
    }

    private static string Prettify(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement, MessageTemplateService.JsonOptions);
    }
}
