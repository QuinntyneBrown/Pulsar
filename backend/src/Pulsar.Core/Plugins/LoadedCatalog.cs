namespace Pulsar.Core.Plugins;

/// <summary>
/// The set of messages currently loaded into the running tool, plus whatever needs
/// disposing when they are unloaded. A data-only manifest plugin has nothing to
/// dispose (<see cref="_unloadHandle"/> is null); a plugin that supplied a compiled
/// adapter — or a legacy <see cref="Pulsar.Contracts.IPulsarPlugin"/> DLL — disposes
/// its isolated load context here.
/// </summary>
public sealed class LoadedCatalog : IDisposable
{
    private readonly IDisposable? _unloadHandle;

    public LoadedCatalog(
        string name,
        string sourcePath,
        IReadOnlyList<CatalogEntry> messages,
        DateTimeOffset loadedAt,
        IDisposable? unloadHandle = null)
    {
        Name = name;
        SourcePath = sourcePath;
        Messages = messages;
        LoadedAt = loadedAt;
        _unloadHandle = unloadHandle;
    }

    /// <summary>Display name of the plugin/catalog, shown in the UI.</summary>
    public string Name { get; }

    /// <summary>The manifest or assembly the catalog was loaded from.</summary>
    public string SourcePath { get; }

    /// <summary>When the catalog was loaded.</summary>
    public DateTimeOffset LoadedAt { get; }

    /// <summary>Every message this catalog exposes.</summary>
    public IReadOnlyList<CatalogEntry> Messages { get; }

    /// <summary>Finds a message by key (case-insensitive), or null if none matches.</summary>
    public CatalogEntry? FindMessage(string key) =>
        Messages.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Unloads any isolated context this catalog owns.</summary>
    public void Dispose() => _unloadHandle?.Dispose();
}
