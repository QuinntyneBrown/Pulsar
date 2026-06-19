using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pulsar.Contracts;

namespace Pulsar.Core.Adapters;

/// <summary>
/// The <see cref="JsonToRedisValue"/> adapters Pulsar ships, so a plugin needs no
/// compiled code for the common cases. A manifest selects one by name.
/// </summary>
public static class BuiltInAdapters
{
    /// <summary>Publishes the edited JSON exactly as typed.</summary>
    public const string Passthrough = "json-passthrough";

    /// <summary>Wraps the payload in a small type/timestamp/correlation envelope.</summary>
    public const string Envelope = "json-envelope";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Identity adapter: the bytes on the wire are the UTF-8 of the edited JSON.</summary>
    public static byte[] JsonPassthrough(string editedJson, MessageContext context) =>
        Encoding.UTF8.GetBytes(editedJson ?? string.Empty);

    /// <summary>
    /// Wraps the payload as
    /// <c>{ messageType, correlationId, emittedAtUnixMs, payload }</c>. This mirrors
    /// the envelope produced by the reference <c>SampleJsonSerializer</c>, but driven
    /// entirely by the edited JSON and the message key — no CLR type involved.
    /// </summary>
    public static byte[] JsonEnvelope(string editedJson, MessageContext context)
    {
        var payload = JsonNode.Parse(editedJson); // throws on malformed JSON → surfaced as a publish failure
        var envelope = new JsonObject
        {
            ["messageType"] = context.Key,
            ["correlationId"] = Guid.NewGuid().ToString(),
            ["emittedAtUnixMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["payload"] = payload,
        };
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    /// <summary>The names of every built-in adapter (for diagnostics / UI).</summary>
    public static IReadOnlyList<string> Names { get; } = new[] { Passthrough, Envelope };

    /// <summary>Resolves a built-in adapter by name (case-insensitive). Returns false if unknown.</summary>
    public static bool TryResolve(string? name, out JsonToRedisValue adapter)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case Passthrough:
                adapter = JsonPassthrough;
                return true;
            case Envelope:
                adapter = JsonEnvelope;
                return true;
            default:
                adapter = null!;
                return false;
        }
    }
}
