namespace Pulsar.Core.Plugins;

/// <summary>
/// Read-only view of the catalog currently loaded into the running tool. Mutating
/// the catalog (load/unload) goes through <see cref="PluginManagementService"/> so
/// running cyclic jobs are always stopped first; that is why this interface
/// deliberately exposes no Load/Unload — consumers that only read inject this.
/// </summary>
public interface IPluginHost
{
    /// <summary>The currently loaded catalog, or <c>null</c> if none is loaded.</summary>
    LoadedCatalog? Current { get; }
}
