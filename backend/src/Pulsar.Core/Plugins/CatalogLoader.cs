namespace Pulsar.Core.Plugins;

/// <summary>
/// The single entry point the host uses to load a catalog, regardless of form. It
/// dispatches by extension: a <c>.json</c> path is a data-only manifest; anything
/// else is treated as a legacy <see cref="Pulsar.Contracts.IPulsarPlugin"/> assembly.
/// </summary>
public sealed class CatalogLoader
{
    private readonly LegacyPluginLoader _legacy;
    private readonly ManifestPluginLoader _manifest;
    private readonly TimeProvider _clock;

    public CatalogLoader(LegacyPluginLoader legacy, ManifestPluginLoader manifest, TimeProvider? clock = null)
    {
        _legacy = legacy;
        _manifest = manifest;
        _clock = clock ?? TimeProvider.System;
    }

    public LoadedCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new PluginLoadException("A plugin path is required.");

        var loadedAt = _clock.GetUtcNow();
        return IsManifest(path)
            ? _manifest.Load(path, loadedAt)
            : _legacy.Load(path, loadedAt);
    }

    private static bool IsManifest(string path) =>
        path.Trim().EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
