using Pulsar.Core.Cyclic;

namespace Pulsar.Core.Plugins;

/// <summary>
/// Coordinates loading a plugin with the rest of the system: any running cyclic
/// jobs (which reference the outgoing plugin's serializer) are stopped first, so
/// a reload never leaves jobs ticking against an unloaded context.
/// </summary>
public sealed class PluginManagementService
{
    private readonly PluginHost _host;
    private readonly CyclicJobManager _jobs;

    // Depends on the concrete PluginHost (not IPluginHost) because it is the only
    // component allowed to mutate the loaded plugin.
    public PluginManagementService(PluginHost host, CyclicJobManager jobs)
    {
        _host = host;
        _jobs = jobs;
    }

    public async Task<LoadedPlugin> LoadAsync(string assemblyPath)
    {
        await _jobs.ClearAllAsync().ConfigureAwait(false);
        return _host.Load(assemblyPath);
    }

    public async Task UnloadAsync()
    {
        await _jobs.ClearAllAsync().ConfigureAwait(false);
        _host.Unload();
    }
}
