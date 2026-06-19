using Pulsar.Contracts;

namespace Pulsar.Core.Plugins;

/// <summary>
/// The deserialized form of a <c>pulsar.plugin.json</c> manifest — the
/// catalog-as-data that replaces a compiled <see cref="IPulsarPlugin"/>.
/// </summary>
/// <param name="Name">Display name of the plugin.</param>
/// <param name="Adapter">
/// The wire-format adapter for every message: a built-in name
/// (<c>json-passthrough</c>, <c>json-envelope</c>) or a reference of the form
/// <c>relative/path.dll!Namespace.Type.Method</c> to a public static method matching
/// <see cref="JsonToRedisValue"/>.
/// </param>
/// <param name="Messages">The messages this plugin exposes.</param>
public sealed record PluginManifest(
    string? Name,
    string? Adapter,
    IReadOnlyList<MessageManifestEntry>? Messages);

/// <summary>One message in a <see cref="PluginManifest"/>.</summary>
/// <param name="Key">Stable, unique identifier.</param>
/// <param name="DisplayName">Friendly name; defaults to <see cref="Key"/> when omitted.</param>
/// <param name="Category">UI grouping; defaults to <see cref="MessageCategory.Other"/>.</param>
/// <param name="DefaultChannel">Redis channel to publish to unless overridden.</param>
/// <param name="Schema">
/// Path to the JSON Schema for this message, relative to the manifest. May carry a
/// JSON-pointer fragment, e.g. <c>messages.schema.json#/$defs/Heartbeat</c>.
/// </param>
/// <param name="Example">
/// Optional path to an example payload (relative to the manifest) used to seed the
/// editor. When omitted, the example is taken from the schema or generated.
/// </param>
public sealed record MessageManifestEntry(
    string? Key,
    string? DisplayName,
    MessageCategory? Category,
    string? DefaultChannel,
    string? Schema,
    string? Example);
