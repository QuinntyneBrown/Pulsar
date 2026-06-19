namespace Pulsar.Core.Plugins;

/// <summary>
/// Owns the lifetime of a plugin's isolated <see cref="PluginLoadContext"/>.
/// Disposing it unloads the context. A <see cref="LoadedCatalog"/> holds one of
/// these only when it actually loaded an assembly (a legacy plugin or a custom
/// adapter); data-only manifests have nothing to unload.
/// </summary>
internal sealed class LoadContextHandle : IDisposable
{
    private readonly PluginLoadContext _context;

    public LoadContextHandle(PluginLoadContext context) => _context = context;

    public void Dispose() => _context.Unload();
}
