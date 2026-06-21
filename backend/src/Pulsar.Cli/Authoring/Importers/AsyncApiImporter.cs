using System.Text.Json.Nodes;
using Pulsar.Core;
using YamlDotNet.Serialization;

namespace Pulsar.Cli.Authoring.Importers;

/// <summary>
/// Imports an AsyncAPI document (YAML or JSON). Each entry under
/// <c>components.messages</c> with a <c>payload</c> (a JSON Schema) becomes one message;
/// its channel is resolved from the <c>channels</c> map when possible, else defaulted.
/// As a fallback, messages defined inline on a channel operation are imported too.
/// </summary>
/// <remarks>
/// This is best-effort and intentionally shallow: <c>$ref</c>s inside a payload are left
/// as-is (Pulsar's advisory validator simply ignores keywords it doesn't understand).
/// YAML is parsed via YamlDotNet and re-emitted as JSON so the rest is plain
/// <see cref="JsonNode"/> walking (JSON input is valid YAML, so both forms work).
/// </remarks>
public sealed class AsyncApiImporter : ISpecImporter
{
    public string Format => "asyncapi";
    public string Description => "An AsyncAPI v2/v3 document (.yaml/.json) → one message per components.messages entry.";

    public PluginDraft Import(Stream spec, ImportOptions options)
    {
        var root = ReadAsJson(spec);
        var channelMap = BuildChannelMap(root);

        var messages = new List<DraftMessage>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Get(Get(root, "components"), "messages") is JsonObject components)
        {
            foreach (var (name, node) in components)
            {
                var payload = Get(node, "payload");
                if (payload is null) continue;
                Add(messages, keys, name, AsString(Get(node, "title")), payload, channelMap, channelOverride: null);
            }
        }

        if (messages.Count == 0 && Get(root, "channels") is JsonObject channels)
        {
            foreach (var (address, channelNode) in channels)
                foreach (var op in new[] { "publish", "subscribe" })
                {
                    var payload = Get(Get(Get(channelNode, op), "message"), "payload");
                    if (payload is null) continue;
                    Add(messages, keys, address, null, payload, channelMap, channelOverride: address);
                }
        }

        if (messages.Count == 0)
            throw new PluginLoadException("No AsyncAPI messages with a payload schema were found.");

        return new PluginDraft(options.PluginName, options.Adapter, messages);
    }

    private static void Add(
        List<DraftMessage> messages, HashSet<string> keys,
        string rawName, string? title, JsonNode payload,
        IReadOnlyDictionary<string, string> channelMap, string? channelOverride)
    {
        var key = Unique(rawName, keys);
        var channel = channelOverride
            ?? (channelMap.TryGetValue(rawName, out var mapped) ? mapped : Naming.DefaultChannel(key, Naming.Categorize(rawName)));
        var category = Naming.Categorize($"{channel} {rawName}");
        messages.Add(new DraftMessage(key, title ?? key, category, channel, payload.ToJsonString()));
    }

    // ---- AsyncAPI structure helpers ---------------------------------------

    private static Dictionary<string, string> BuildChannelMap(JsonObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Get(root, "channels") is not JsonObject channels) return map;

        foreach (var (address, channelNode) in channels)
        {
            // AsyncAPI 2.x: channels.<addr>.{publish|subscribe}.message.$ref
            foreach (var op in new[] { "publish", "subscribe" })
            {
                var name = RefName(AsString(Get(Get(Get(channelNode, op), "message"), "$ref")));
                if (name is not null) map[name] = address;
            }

            // AsyncAPI 3.x: channels.<addr>.messages.<name>(.$ref)
            if (Get(channelNode, "messages") is JsonObject channelMessages)
                foreach (var (messageName, messageNode) in channelMessages)
                    map[RefName(AsString(Get(messageNode, "$ref"))) ?? messageName] = address;
        }
        return map;
    }

    private static string? RefName(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var slash = reference.LastIndexOf('/');
        return slash >= 0 ? reference[(slash + 1)..] : reference;
    }

    // ---- safe JsonNode navigation -----------------------------------------

    private static JsonNode? Get(JsonNode? node, string key) => (node as JsonObject)?[key];

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    private static string Unique(string raw, HashSet<string> used)
    {
        var key = string.IsNullOrWhiteSpace(raw) ? "Message" : raw.Trim();
        if (used.Add(key)) return key;
        for (var i = 2; ; i++)
        {
            var candidate = $"{key}{i}";
            if (used.Add(candidate)) return candidate;
        }
    }

    // ---- YAML/JSON loading -------------------------------------------------

    private static JsonObject ReadAsJson(Stream spec)
    {
        string text;
        using (var reader = new StreamReader(spec))
            text = reader.ReadToEnd();

        object? graph;
        try
        {
            graph = new DeserializerBuilder().Build().Deserialize(new StringReader(text));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new PluginLoadException($"Input is not valid YAML/JSON: {ex.Message}", ex);
        }

        if (graph is null)
            throw new PluginLoadException("The AsyncAPI document is empty.");

        var json = new SerializerBuilder().JsonCompatible().Build().Serialize(graph);
        try
        {
            return JsonNode.Parse(json)?.AsObject()
                ?? throw new PluginLoadException("The AsyncAPI document is not a JSON object at its root.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new PluginLoadException($"Could not parse the AsyncAPI document: {ex.Message}", ex);
        }
    }
}
