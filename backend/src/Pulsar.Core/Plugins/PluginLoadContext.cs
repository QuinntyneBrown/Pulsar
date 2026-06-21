using System.Reflection;
using System.Runtime.Loader;

namespace Pulsar.Core.Plugins;

/// <summary>
/// Isolated load context for a custom adapter assembly and its private dependencies.
/// </summary>
/// <remarks>
/// Shared, identity-sensitive assemblies — first and foremost
/// <c>Pulsar.Contracts</c>, plus all framework assemblies — are deliberately
/// <em>not</em> loaded here. Returning <c>null</c> from <see cref="Load"/> defers
/// to the default load context, so the <see cref="Pulsar.Contracts.JsonToRedisValue"/>
/// delegate type is shared between the host and the adapter.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(name: $"Pulsar.Plugin({Path.GetFileNameWithoutExtension(pluginPath)})", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == "Pulsar.Contracts")
            return null; // defer to the default context for the shared contract

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null) return null;

        // Load from a stream so the dependency file is not locked on disk (Windows
        // memory-maps and locks assemblies loaded by path), keeping the
        // rebuild-and-reload loop working.
        using var stream = File.OpenRead(path);
        return LoadFromStream(stream);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
