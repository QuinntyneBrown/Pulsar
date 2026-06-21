namespace Pulsar.Core.Plugins;

/// <summary>
/// The single entry point the host uses to load a catalog from a data-only
/// <c>pulsar.plugin.json</c> manifest.
/// </summary>
public sealed class CatalogLoader
{
    private readonly ManifestPluginLoader _manifest;
    private readonly TimeProvider _clock;

    public CatalogLoader(ManifestPluginLoader manifest, TimeProvider? clock = null)
    {
        _manifest = manifest;
        _clock = clock ?? TimeProvider.System;
    }

    public LoadedCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new PluginLoadException("A plugin path is required.");

        var loadedAt = _clock.GetUtcNow();
        if (!path.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new PluginLoadException("Pulsar only loads data-only manifest plugins. Provide a pulsar.plugin.json path.");

        return _manifest.Load(path, loadedAt);
    }
}
