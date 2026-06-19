using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;

namespace Pulsar.Api.Contracts;

// ---- Requests --------------------------------------------------------------

public sealed record LoadPluginRequest(string Path);

public sealed record PublishRequest(string Key, string? Channel, string PayloadJson);

public sealed record StartCyclicRequest(string Key, string? Channel, int IntervalMs, string PayloadJson);

public sealed record SetConnectionRequest(string ConnectionString);

public sealed record ValidateMessageRequest(string PayloadJson);

// ---- Responses -------------------------------------------------------------

public sealed record PluginStateDto(bool IsLoaded, PluginInfoDto? Plugin);

public sealed record PluginInfoDto(string Name, string SourcePath, DateTimeOffset LoadedAt, int MessageCount)
{
    public static PluginInfoDto From(LoadedCatalog c) =>
        new(c.Name, c.SourcePath, c.LoadedAt, c.Messages.Count);
}

public sealed record MessageDto(string Key, string DisplayName, string Category, string DefaultChannel)
{
    public static MessageDto From(CatalogEntry e) =>
        new(e.Key, e.DisplayName, e.Category.ToString(), e.DefaultChannel);
}

public sealed record MessageDetailDto(
    string Key,
    string DisplayName,
    string Category,
    string DefaultChannel,
    string MessageType,
    string TemplateJson,
    bool HasSchema)
{
    public static MessageDetailDto From(CatalogEntry e, string templateJson) =>
        new(e.Key, e.DisplayName, e.Category.ToString(), e.DefaultChannel,
            e.TypeHint ?? e.Key, templateJson, e.Schema is not null);
}

/// <summary>Advisory schema-check result returned to the composer; never blocks publishing.</summary>
public sealed record ValidationResultDto(bool Matches, IReadOnlyList<string> Messages)
{
    public static ValidationResultDto From(ValidationResult r) => new(r.Matches, r.Messages);
}
