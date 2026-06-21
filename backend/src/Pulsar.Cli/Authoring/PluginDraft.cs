using Pulsar.Contracts;

namespace Pulsar.Cli.Authoring;

/// <summary>
/// One message in a <see cref="PluginDraft"/>: enough to emit a schema file, an
/// example file, and a manifest entry. The schema and example are carried as JSON
/// text so importers stay decoupled from how they are laid out on disk —
/// <see cref="PluginWriter"/> owns the layout.
/// </summary>
public sealed record DraftMessage(
    string Key,
    string DisplayName,
    MessageCategory Category,
    string DefaultChannel,
    string SchemaJson,
    string? ExampleJson = null);

/// <summary>
/// An in-memory, format-agnostic description of a data-only plugin, produced by
/// <c>new</c> or any <see cref="ISpecImporter"/> and written to disk by
/// <see cref="PluginWriter"/>.
/// </summary>
public sealed record PluginDraft(string Name, string Adapter, IReadOnlyList<DraftMessage> Messages);
