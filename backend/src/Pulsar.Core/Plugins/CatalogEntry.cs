using Pulsar.Contracts;
using Pulsar.Core.Messages;

namespace Pulsar.Core.Plugins;

/// <summary>
/// One publishable message in a loaded manifest catalog. A catalog entry carries
/// <em>no CLR type</em>: the editor template is JSON, and the bytes on the wire come
/// from <see cref="Adapter"/>.
/// </summary>
public sealed class CatalogEntry
{
    private readonly Func<string> _createTemplateJson;

    public CatalogEntry(
        string key,
        string displayName,
        MessageCategory category,
        string defaultChannel,
        Func<string> createTemplateJson,
        JsonToRedisValue adapter,
        CompiledSchema? schema = null,
        string? typeHint = null)
    {
        Key = Require(key, nameof(key));
        DisplayName = Require(displayName, nameof(displayName));
        Category = category;
        DefaultChannel = Require(defaultChannel, nameof(defaultChannel));
        _createTemplateJson = createTemplateJson ?? throw new ArgumentNullException(nameof(createTemplateJson));
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        Schema = schema;
        TypeHint = typeHint;
    }

    /// <summary>Stable, unique identifier used by the API and UI.</summary>
    public string Key { get; }

    /// <summary>Human-friendly name shown in the UI.</summary>
    public string DisplayName { get; }

    /// <summary>Grouping hint for the UI.</summary>
    public MessageCategory Category { get; }

    /// <summary>The Redis channel this message publishes to unless overridden.</summary>
    public string DefaultChannel { get; }

    /// <summary>
    /// A subtitle hint for the UI, usually the schema title. Purely presentational.
    /// </summary>
    public string? TypeHint { get; }

    /// <summary>Turns the user's edited JSON into the bytes published on the wire.</summary>
    public JsonToRedisValue Adapter { get; }

    /// <summary>Optional schema for advisory validation; <c>null</c> when none was supplied.</summary>
    public CompiledSchema? Schema { get; }

    /// <summary>The JSON the editor opens with (a realistic example payload).</summary>
    public string CreateTemplateJson() => _createTemplateJson();

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"'{name}' must not be null or blank.", name)
            : value;
}
