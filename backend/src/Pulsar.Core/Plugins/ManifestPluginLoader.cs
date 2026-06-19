using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Contracts;
using Pulsar.Core.Adapters;
using Pulsar.Core.Messages;

namespace Pulsar.Core.Plugins;

/// <summary>
/// Loads a data-only plugin from a <c>pulsar.plugin.json</c> manifest: JSON Schemas
/// describe the messages, an example seeds each editor, and a named adapter (built-in
/// or a referenced static method) turns edited JSON into wire bytes. No
/// <see cref="IPulsarPlugin"/>, and — for the built-in adapters — no compiled code
/// and therefore no assembly to load.
/// </summary>
public sealed class ManifestPluginLoader
{
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger<ManifestPluginLoader> _logger;

    public ManifestPluginLoader(ILogger<ManifestPluginLoader>? logger = null)
        => _logger = logger ?? NullLogger<ManifestPluginLoader>.Instance;

    public LoadedCatalog Load(string manifestPath, DateTimeOffset loadedAt)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            throw new PluginLoadException("A manifest path is required.");

        var fullPath = Path.GetFullPath(manifestPath.Trim());
        if (!File.Exists(fullPath))
            throw new PluginLoadException($"Manifest not found: {fullPath}");

        var baseDir = Path.GetDirectoryName(fullPath)!;

        PluginManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(fullPath), ManifestJson)
                ?? throw new PluginLoadException($"Manifest '{fullPath}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new PluginLoadException($"Manifest '{fullPath}' is not valid JSON: {ex.Message}", ex);
        }

        if (manifest.Messages is null || manifest.Messages.Count == 0)
            throw new PluginLoadException($"Manifest '{fullPath}' declares no messages.");

        var name = string.IsNullOrWhiteSpace(manifest.Name) ? Path.GetFileName(fullPath) : manifest.Name!;

        var (adapter, unloadHandle) = ResolveAdapter(manifest.Adapter, baseDir, fullPath);

        try
        {
            var entries = new List<CatalogEntry>(manifest.Messages.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var message in manifest.Messages)
            {
                if (string.IsNullOrWhiteSpace(message.Key))
                    throw new PluginLoadException($"Manifest '{fullPath}' has a message with no key.");
                if (!seen.Add(message.Key!))
                    throw new PluginLoadException($"Manifest '{fullPath}' has duplicate message key '{message.Key}'.");
                if (string.IsNullOrWhiteSpace(message.DefaultChannel))
                    throw new PluginLoadException($"Message '{message.Key}' has no defaultChannel.");

                var schema = LoadSchema(message, baseDir, fullPath);
                var templateJson = ResolveExample(message, schema, baseDir, fullPath);

                // Advisory: warn if the seed example does not satisfy its own schema,
                // but never fail the load — the example is only a starting point.
                if (schema is not null)
                {
                    var check = SchemaValidator.Validate(templateJson, schema);
                    if (!check.Matches)
                        _logger.LogWarning("Example for '{Key}' does not match its schema: {Issues}",
                            message.Key, string.Join("; ", check.Messages));
                }

                entries.Add(new CatalogEntry(
                    key: message.Key!,
                    displayName: string.IsNullOrWhiteSpace(message.DisplayName) ? message.Key! : message.DisplayName!,
                    category: message.Category ?? MessageCategory.Other,
                    defaultChannel: message.DefaultChannel!,
                    createTemplateJson: () => templateJson,
                    adapter: adapter,
                    schema: schema,
                    typeHint: SchemaTitle(schema)));
            }

            _logger.LogInformation("Loaded manifest plugin '{Name}' from {Path} with {Count} message(s), adapter '{Adapter}'.",
                name, fullPath, entries.Count, manifest.Adapter);

            return new LoadedCatalog(name, fullPath, entries, loadedAt, unloadHandle);
        }
        catch
        {
            unloadHandle?.Dispose();
            throw;
        }
    }

    // ---- adapter resolution ------------------------------------------------

    private static (JsonToRedisValue Adapter, IDisposable? Unload) ResolveAdapter(
        string? reference, string baseDir, string manifestPath)
    {
        // Default to passthrough when unspecified: a manifest with no adapter still works.
        var name = string.IsNullOrWhiteSpace(reference) ? BuiltInAdapters.Passthrough : reference!.Trim();

        if (BuiltInAdapters.TryResolve(name, out var builtIn))
            return (builtIn, null);

        // Otherwise it must be an "assembly.dll!Namespace.Type.Method" reference.
        var bang = name.IndexOf('!');
        if (bang <= 0 || bang == name.Length - 1)
            throw new PluginLoadException(
                $"Unknown adapter '{name}'. Use a built-in ({string.Join(", ", BuiltInAdapters.Names)}) " +
                "or 'relative/path.dll!Namespace.Type.Method'.");

        var assemblyRef = name[..bang].Trim();
        var memberRef = name[(bang + 1)..].Trim();
        var lastDot = memberRef.LastIndexOf('.');
        if (lastDot <= 0)
            throw new PluginLoadException($"Adapter reference '{name}' must be 'Assembly.dll!Namespace.Type.Method'.");

        var typeName = memberRef[..lastDot];
        var methodName = memberRef[(lastDot + 1)..];

        var assemblyPath = Path.IsPathRooted(assemblyRef) ? assemblyRef : Path.Combine(baseDir, assemblyRef);
        assemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(assemblyPath))
            throw new PluginLoadException($"Adapter assembly not found: {assemblyPath} (from manifest '{manifestPath}').");

        var context = new PluginLoadContext(assemblyPath);
        try
        {
            Assembly assembly;
            using (var stream = File.OpenRead(assemblyPath))
                assembly = context.LoadFromStream(stream);

            var type = assembly.GetType(typeName)
                ?? throw new PluginLoadException($"Adapter type '{typeName}' not found in '{assemblyPath}'.");

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new PluginLoadException($"Adapter method '{methodName}' (public static) not found on '{typeName}'.");

            JsonToRedisValue adapter;
            try
            {
                adapter = (JsonToRedisValue)Delegate.CreateDelegate(typeof(JsonToRedisValue), method);
            }
            catch (Exception ex)
            {
                throw new PluginLoadException(
                    $"Adapter '{typeName}.{methodName}' does not match 'byte[] (string, MessageContext)': {ex.Message}", ex);
            }

            return (adapter, new LoadContextHandle(context));
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    // ---- schema + example resolution --------------------------------------

    private static CompiledSchema? LoadSchema(MessageManifestEntry message, string baseDir, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(message.Schema))
            return null;

        var (relative, pointer) = SplitPointer(message.Schema!);
        var schemaPath = Path.GetFullPath(Path.Combine(baseDir, relative));
        if (!File.Exists(schemaPath))
            throw new PluginLoadException($"Schema not found for '{message.Key}': {schemaPath} (from manifest '{manifestPath}').");

        try
        {
            return CompiledSchema.ParseWithPointer(File.ReadAllText(schemaPath), pointer);
        }
        catch (JsonException ex)
        {
            throw new PluginLoadException($"Schema for '{message.Key}' is not valid JSON: {ex.Message}", ex);
        }
    }

    private string ResolveExample(MessageManifestEntry message, CompiledSchema? schema, string baseDir, string manifestPath)
    {
        // 1. An explicit example file wins.
        if (!string.IsNullOrWhiteSpace(message.Example))
        {
            var examplePath = Path.GetFullPath(Path.Combine(baseDir, message.Example!));
            if (!File.Exists(examplePath))
                throw new PluginLoadException($"Example not found for '{message.Key}': {examplePath} (from manifest '{manifestPath}').");
            return Prettify(File.ReadAllText(examplePath));
        }

        if (schema is not null)
        {
            // 2. The schema's first `examples` entry.
            if (schema.Root.TryGetProperty("examples", out var examples)
                && examples.ValueKind == JsonValueKind.Array && examples.GetArrayLength() > 0)
                return Prettify(examples[0].GetRawText());

            // 3. The schema's `default`, when it is an object.
            if (schema.Root.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.Object)
                return Prettify(def.GetRawText());

            // 4. A skeleton built from each property's default/type.
            var skeleton = BuildSkeleton(schema.Root);
            if (skeleton is not null)
                return Prettify(skeleton.ToJsonString());
        }

        // 5. Nothing to go on.
        return "{}";
    }

    private static JsonObject? BuildSkeleton(JsonElement schema)
    {
        if (!schema.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;

        var obj = new JsonObject();
        foreach (var prop in props.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("default", out var def))
            {
                obj[prop.Name] = JsonNode.Parse(def.GetRawText());
                continue;
            }

            var type = prop.Value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
            obj[prop.Name] = type switch
            {
                "string" => "",
                "integer" or "number" => (JsonNode)0,
                "boolean" => false,
                "array" => new JsonArray(),
                "object" => new JsonObject(),
                _ => null,
            };
        }
        return obj;
    }

    private static (string Relative, string? Pointer) SplitPointer(string reference)
    {
        var hash = reference.IndexOf('#');
        return hash < 0 ? (reference, null) : (reference[..hash], reference[hash..]);
    }

    private static string? SchemaTitle(CompiledSchema? schema) =>
        schema is not null && schema.Root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
            ? title.GetString()
            : null;

    private static string Prettify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, MessageTemplateService.JsonOptions);
        }
        catch (JsonException)
        {
            return json; // leave it as-is; the editor will still show it and validation will flag it.
        }
    }
}
