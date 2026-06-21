using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Readers;
using Pulsar.Core;

namespace Pulsar.Cli.Authoring.Importers;

/// <summary>
/// Imports an OpenAPI document (v2/v3, JSON or YAML). Each schema under
/// <c>components.schemas</c> becomes one message; the schema is serialized back out as
/// JSON for the message's schema file. (OpenAPI 3.0's schema dialect is close enough to
/// JSON Schema for Pulsar's advisory validation.)
/// </summary>
public sealed class OpenApiImporter : ISpecImporter
{
    public string Format => "openapi";
    public string Description => "An OpenAPI v2/v3 document (.json/.yaml) → one message per component schema.";

    public PluginDraft Import(Stream spec, ImportOptions options)
    {
        var document = new OpenApiStreamReader().Read(spec, out var diagnostic);
        if (document is null)
            throw new PluginLoadException("The OpenAPI document could not be read.");
        if (diagnostic?.Errors is { Count: > 0 } errors)
            throw new PluginLoadException($"OpenAPI document has errors: {string.Join("; ", errors.Select(e => e.Message))}");

        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
            throw new PluginLoadException("The OpenAPI document declares no component schemas to import.");

        var messages = new List<DraftMessage>(schemas.Count);
        foreach (var (name, schema) in schemas)
        {
            var json = schema.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
            var category = Naming.Categorize(name);
            messages.Add(new DraftMessage(
                Key: name,
                DisplayName: name,
                Category: category,
                DefaultChannel: Naming.DefaultChannel(name, category),
                SchemaJson: json));
        }

        return new PluginDraft(options.PluginName, options.Adapter, messages);
    }
}
