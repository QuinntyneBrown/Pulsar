using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Contracts;

namespace Pulsar.Core.Plugins;

/// <summary>
/// Loads a plugin assembly from disk into an isolated context and locates its
/// single <see cref="IPulsarPlugin"/> implementation.
/// </summary>
public sealed class PluginLoader
{
    private readonly TimeProvider _clock;
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(TimeProvider? clock = null, ILogger<PluginLoader>? logger = null)
    {
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PluginLoader>.Instance;
    }

    public LoadedPlugin Load(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new PluginLoadException("A plugin assembly path is required.");

        var fullPath = Path.GetFullPath(assemblyPath.Trim());
        if (!File.Exists(fullPath))
            throw new PluginLoadException($"Plugin assembly not found: {fullPath}");

        var context = new PluginLoadContext(fullPath);
        try
        {
            // Load from a stream rather than by path so the DLL is not locked on
            // disk — that lock would otherwise block rebuilding the plugin and
            // reloading it, which is the whole point of the tool.
            Assembly assembly;
            using (var stream = File.OpenRead(fullPath))
                assembly = context.LoadFromStream(stream);

            var pluginType = FindPluginType(assembly, fullPath);

            IPulsarPlugin plugin;
            try
            {
                plugin = (IPulsarPlugin)Activator.CreateInstance(pluginType)!;
            }
            catch (Exception ex)
            {
                var cause = (ex as TargetInvocationException)?.InnerException ?? ex;
                var hint = cause is MissingMethodException or MemberAccessException
                    ? " Ensure it has a public parameterless constructor."
                    : string.Empty;
                throw new PluginLoadException(
                    $"Failed to instantiate plugin type '{pluginType.FullName}': {cause.Message}{hint}", cause);
            }

            ValidatePlugin(plugin);

            _logger.LogInformation("Loaded plugin '{Name}' from {Path} with {Count} message(s).",
                plugin.Name, fullPath, plugin.Catalog.Messages.Count);

            return new LoadedPlugin(plugin, context, fullPath, _clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            context.Unload();
            if (ex is PluginLoadException) throw;
            throw new PluginLoadException($"Failed to load plugin from '{fullPath}': {ex.Message}", ex);
        }
    }

    private static Type FindPluginType(Assembly assembly, string path)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var detail = string.Join("; ", ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message).Distinct());
            throw new PluginLoadException(
                $"Could not inspect types in '{path}'. A dependency may be missing. {detail}", ex);
        }

        var matches = types
            .Where(t => t is not null && typeof(IPulsarPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Cast<Type>()
            .ToList();

        return matches.Count switch
        {
            0 => throw new PluginLoadException(
                $"No public type implementing IPulsarPlugin was found in '{path}'."),
            > 1 => throw new PluginLoadException(
                $"Multiple IPulsarPlugin implementations found in '{path}': {string.Join(", ", matches.Select(t => t.FullName))}. Expected exactly one."),
            _ => matches[0],
        };
    }

    private static void ValidatePlugin(IPulsarPlugin plugin)
    {
        if (plugin.Catalog is null)
            throw new PluginLoadException($"Plugin '{plugin.Name}' returned a null catalog.");
        if (plugin.Serializer is null)
            throw new PluginLoadException($"Plugin '{plugin.Name}' returned a null serializer.");

        var duplicate = plugin.Catalog.Messages
            .GroupBy(m => m.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new PluginLoadException($"Plugin '{plugin.Name}' has duplicate message key '{duplicate.Key}'.");
    }
}
