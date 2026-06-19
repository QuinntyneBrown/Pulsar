using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Contracts;
using Pulsar.Core.Messages;
using Pulsar.Core.Publishing;

namespace Pulsar.Core.Plugins;

/// <summary>
/// Back-compat loader for the original model: loads a .NET assembly implementing
/// <see cref="IPulsarPlugin"/> and adapts it to a <see cref="LoadedCatalog"/>. Each
/// legacy message becomes a <see cref="CatalogEntry"/> whose adapter is exactly the
/// old pipeline tail — <c>JSON → rehydrate to the CLR type → IMessageSerializer.Serialize</c> —
/// proving the new <see cref="JsonToRedisValue"/> model is a strict generalization.
/// </summary>
public sealed class LegacyPluginLoader
{
    private readonly ILogger<LegacyPluginLoader> _logger;

    public LegacyPluginLoader(ILogger<LegacyPluginLoader>? logger = null)
        => _logger = logger ?? NullLogger<LegacyPluginLoader>.Instance;

    public LoadedCatalog Load(string assemblyPath, DateTimeOffset loadedAt)
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

            var entries = plugin.Catalog.Messages.Select(d => ToEntry(d, plugin.Serializer)).ToList();

            _logger.LogInformation("Loaded legacy plugin '{Name}' from {Path} with {Count} message(s).",
                plugin.Name, fullPath, entries.Count);

            return new LoadedCatalog(plugin.Name, fullPath, entries, loadedAt, new LoadContextHandle(context));
        }
        catch (Exception ex)
        {
            context.Unload();
            if (ex is PluginLoadException) throw;
            throw new PluginLoadException($"Failed to load plugin from '{fullPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Adapts one legacy <see cref="MessageDescriptor"/> into a <see cref="CatalogEntry"/>:
    /// the template is the reflected POCO serialized to JSON, and the adapter
    /// rehydrates that JSON into the CLR type before handing it to the plugin's
    /// serializer.
    /// </summary>
    internal static CatalogEntry ToEntry(MessageDescriptor descriptor, IMessageSerializer serializer)
    {
        JsonToRedisValue adapter = (json, _) =>
        {
            object instance;
            try
            {
                instance = JsonSerializer.Deserialize(json, descriptor.MessageType, MessageTemplateService.JsonOptions)
                    ?? throw new MessageEditException("Payload JSON deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new MessageEditException(
                    $"Payload JSON is not valid for '{descriptor.DisplayName}': {ex.Message}", ex);
            }

            return serializer.Serialize(instance, descriptor);
        };

        return new CatalogEntry(
            key: descriptor.Key,
            displayName: descriptor.DisplayName,
            category: descriptor.Category,
            defaultChannel: descriptor.DefaultChannel,
            createTemplateJson: () => ReflectTemplateJson(descriptor),
            adapter: adapter,
            schema: null,
            typeHint: descriptor.MessageType.Name);
    }

    private static string ReflectTemplateJson(MessageDescriptor descriptor)
    {
        object instance;
        try
        {
            instance = descriptor.CreateTemplate()
                ?? throw new MessageEditException($"The template factory for '{descriptor.Key}' returned null.");
        }
        catch (MessageEditException) { throw; }
        catch (Exception ex)
        {
            throw new MessageEditException($"The template factory for '{descriptor.Key}' threw: {ex.Message}", ex);
        }

        return JsonSerializer.Serialize(instance, instance.GetType(), MessageTemplateService.JsonOptions);
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
