using Pulsar.Contracts;
using Pulsar.Core.Plugins;

namespace Pulsar.Api.Contracts;

// ---- Requests --------------------------------------------------------------

public sealed record LoadPluginRequest(string Path);

public sealed record PublishRequest(string Key, string? Channel, string PayloadJson);

public sealed record StartCyclicRequest(string Key, string? Channel, int IntervalMs, string PayloadJson);

public sealed record SetConnectionRequest(string ConnectionString);

// ---- Responses -------------------------------------------------------------

public sealed record PluginStateDto(bool IsLoaded, PluginInfoDto? Plugin);

public sealed record PluginInfoDto(string Name, string SourcePath, DateTimeOffset LoadedAt, int MessageCount)
{
    public static PluginInfoDto From(LoadedPlugin p) =>
        new(p.Name, p.SourcePath, p.LoadedAt, p.Messages.Count);
}

public sealed record MessageDto(string Key, string DisplayName, string Category, string DefaultChannel)
{
    public static MessageDto From(MessageDescriptor d) =>
        new(d.Key, d.DisplayName, d.Category.ToString(), d.DefaultChannel);
}

public sealed record MessageDetailDto(
    string Key,
    string DisplayName,
    string Category,
    string DefaultChannel,
    string MessageType,
    string TemplateJson)
{
    public static MessageDetailDto From(MessageDescriptor d, string templateJson) =>
        new(d.Key, d.DisplayName, d.Category.ToString(), d.DefaultChannel, d.MessageType.Name, templateJson);
}
