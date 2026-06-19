namespace Pulsar.Contracts;

/// <summary>
/// Turns a message payload into the exact bytes that go onto Redis Pub/Sub.
/// </summary>
/// <remarks>
/// This is the heart of Pulsar's extensibility: the tool does not know the wire
/// format of the system under test, so the plugin supplies it. An implementation
/// is free to wrap the payload in an envelope, add a type discriminator, stamp a
/// timestamp/correlation id, compress, encrypt — whatever the downstream
/// consumer expects — and return the resulting bytes.
/// </remarks>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes <paramref name="message"/> (an instance of
    /// <see cref="MessageDescriptor.MessageType"/>) into bytes ready to publish.
    /// </summary>
    /// <param name="message">The payload instance to serialize.</param>
    /// <param name="descriptor">
    /// The descriptor the message was produced from, in case the wire format
    /// needs the message key, channel, or type.
    /// </param>
    byte[] Serialize(object message, MessageDescriptor descriptor);
}
