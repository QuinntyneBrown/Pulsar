using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsar.Core.Plugins;

namespace Pulsar.Core.Messages;

/// <summary>
/// Helpers for the editor side of a message: the JSON the editor opens with and the
/// advisory schema check shown beside it. In the JSON-standardized model Pulsar no
/// longer rehydrates a CLR type — the edited JSON goes straight to the adapter — so
/// this service is thin: it surfaces a catalog entry's example and validates against
/// its schema (when one was supplied).
/// </summary>
public sealed class MessageTemplateService
{
    /// <summary>
    /// JSON settings for the editor view: camelCase + case-insensitive matching,
    /// indented, enums as readable strings. Shared by the loaders that prettify
    /// examples and by the legacy shim that reflects a POCO to JSON.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The JSON the editor opens with for a message.</summary>
    public string CreateTemplateJson(CatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.CreateTemplateJson();
    }

    /// <summary>
    /// Validates a payload against the entry's schema. Returns
    /// <see cref="ValidationResult.Ok"/> when the entry has no schema — validation is
    /// advisory and absence of a schema simply means "nothing to check".
    /// </summary>
    public ValidationResult Validate(CatalogEntry entry, string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Schema is null ? ValidationResult.Ok : SchemaValidator.Validate(payloadJson, entry.Schema);
    }
}
