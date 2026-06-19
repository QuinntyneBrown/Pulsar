namespace Pulsar.Contracts;

/// <summary>
/// A ready-made <see cref="IMessageCatalog"/> backed by a list. Plugin authors
/// can use this directly instead of writing their own implementation.
/// </summary>
public sealed class MessageCatalog : IMessageCatalog
{
    private readonly List<MessageDescriptor> _messages = new();

    public MessageCatalog() { }

    public MessageCatalog(IEnumerable<MessageDescriptor> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
            Add(message);
    }

    public IReadOnlyList<MessageDescriptor> Messages => _messages;

    /// <summary>Adds a descriptor. Keys must be unique within a catalog.</summary>
    public MessageCatalog Add(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (_messages.Any(m => string.Equals(m.Key, descriptor.Key, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A message with key '{descriptor.Key}' is already registered.", nameof(descriptor));
        _messages.Add(descriptor);
        return this;
    }
}
