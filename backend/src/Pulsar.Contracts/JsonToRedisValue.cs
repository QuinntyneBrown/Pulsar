namespace Pulsar.Contracts;

/// <summary>
/// The entire required contract for a message library in Pulsar's JSON-standardized
/// model: turn the user's edited JSON into the exact bytes published on Redis
/// Pub/Sub.
/// </summary>
/// <remarks>
/// This seam receives the already-edited JSON string rather than a rehydrated CLR
/// object, so a plugin does not need to declare message types. Pulsar ships built-in adapters
/// (<c>json-passthrough</c>, <c>json-envelope</c>) that cover plain or enveloped
/// JSON with no code at all. Supply a custom adapter — a <c>public static</c> method
/// matching this delegate, referenced from the manifest as
/// <c>relative/path.dll!Namespace.Type.Method</c> — only when the wire format is
/// non-trivial (binary, protobuf, a bespoke framing); such an adapter is free to
/// parse the JSON and emit any bytes it likes.
/// <para>
/// The delegate returns raw <see cref="byte"/>s rather than a Redis-specific type so
/// the contract stays free of any Redis client dependency; the bytes become the
/// published value at the transport edge.
/// </para>
/// </remarks>
/// <param name="editedJson">The payload JSON exactly as edited in the UI.</param>
/// <param name="context">Key, resolved channel, and category for this message.</param>
public delegate byte[] JsonToRedisValue(string editedJson, MessageContext context);
