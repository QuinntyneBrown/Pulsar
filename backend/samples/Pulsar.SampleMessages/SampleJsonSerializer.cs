using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Pulsar.Contracts;

namespace Pulsar.SampleMessages;

/// <summary>
/// Example serializer: wraps the payload in a small envelope (type discriminator,
/// correlation id, emit timestamp) and emits camelCase JSON. This is the exact
/// seam you replace to match the wire format your system under test expects — the
/// envelope shape, field names, and encoding are entirely up to you here.
/// </summary>
public sealed class SampleJsonSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public byte[] Serialize(object message, MessageDescriptor descriptor)
    {
        // Serialize the payload using its runtime type so all properties are written.
        var payload = JsonSerializer.SerializeToNode(message, message.GetType(), Options);

        var envelope = new JsonObject
        {
            ["messageType"] = descriptor.Key,
            ["correlationId"] = Guid.NewGuid().ToString(),
            ["emittedAtUnixMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["payload"] = payload,
        };

        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }
}
