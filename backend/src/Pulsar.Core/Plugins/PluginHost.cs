namespace Pulsar.Core.Plugins;

/// <summary>Thread-safe, in-memory implementation of <see cref="IPluginHost"/>.</summary>
public sealed class PluginHost : IPluginHost, IDisposable
{
    private readonly CatalogLoader _loader;
    private readonly object _gate = new();
    private LoadedCatalog? _current;

    public PluginHost(CatalogLoader loader) => _loader = loader;

    public LoadedCatalog? Current
    {
        get { lock (_gate) return _current; }
    }

    public LoadedCatalog Load(string path)
    {
        var loaded = _loader.Load(path);
        lock (_gate)
        {
            _current?.Dispose();
            _current = loaded;
        }
        return loaded;
    }

    public void Unload()
    {
        lock (_gate)
        {
            _current?.Dispose();
            _current = null;
        }
    }

    public void Dispose() => Unload();
}
