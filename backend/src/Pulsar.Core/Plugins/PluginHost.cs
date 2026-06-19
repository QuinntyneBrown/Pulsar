namespace Pulsar.Core.Plugins;

/// <summary>Thread-safe, in-memory implementation of <see cref="IPluginHost"/>.</summary>
public sealed class PluginHost : IPluginHost, IDisposable
{
    private readonly PluginLoader _loader;
    private readonly object _gate = new();
    private LoadedPlugin? _current;

    public PluginHost(PluginLoader loader) => _loader = loader;

    public LoadedPlugin? Current
    {
        get { lock (_gate) return _current; }
    }

    public LoadedPlugin Load(string assemblyPath)
    {
        var loaded = _loader.Load(assemblyPath);
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
