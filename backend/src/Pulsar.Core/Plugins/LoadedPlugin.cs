using Pulsar.Contracts;

namespace Pulsar.Core.Plugins;

/// <summary>
/// A plugin that has been loaded into the host, together with the isolated
/// context it lives in. Disposing unloads that context.
/// </summary>
public sealed class LoadedPlugin : IDisposable
{
    private readonly PluginLoadContext _context;

    internal LoadedPlugin(IPulsarPlugin plugin, PluginLoadContext context, string sourcePath, DateTimeOffset loadedAt)
    {
        Plugin = plugin;
        _context = context;
        SourcePath = sourcePath;
        LoadedAt = loadedAt;
    }

    public IPulsarPlugin Plugin { get; }
    public string Name => Plugin.Name;
    public string SourcePath { get; }
    public DateTimeOffset LoadedAt { get; }
    public IMessageSerializer Serializer => Plugin.Serializer;
    public IReadOnlyList<MessageDescriptor> Messages => Plugin.Catalog.Messages;

    public MessageDescriptor? FindMessage(string key) =>
        Messages.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => _context.Unload();
}
