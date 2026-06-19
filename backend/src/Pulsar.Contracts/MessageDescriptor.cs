namespace Pulsar.Contracts;

/// <summary>
/// Describes one publishable message. A plugin exposes one descriptor per
/// message type it wants Pulsar to be able to generate.
/// </summary>
/// <remarks>
/// Pulsar treats the message body as an opaque CLR object. It seeds an editor
/// from <see cref="CreateTemplate"/>, lets the user edit the payload as JSON,
/// rehydrates an instance of <see cref="MessageType"/> from that JSON, and then
/// hands the instance to <see cref="IMessageSerializer.Serialize"/>. Pulsar
/// never inspects the wire format — that is entirely the plugin's concern.
/// </remarks>
public sealed class MessageDescriptor
{
    public MessageDescriptor(
        string key,
        string displayName,
        MessageCategory category,
        Type messageType,
        string defaultChannel,
        Func<object> createTemplate)
    {
        Key = Require(key, nameof(key));
        DisplayName = Require(displayName, nameof(displayName));
        Category = category;
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        DefaultChannel = Require(defaultChannel, nameof(defaultChannel));
        CreateTemplate = createTemplate ?? throw new ArgumentNullException(nameof(createTemplate));
    }

    /// <summary>Stable, unique identifier for this message (used by the API and UI).</summary>
    public string Key { get; }

    /// <summary>Human-friendly name shown in the UI.</summary>
    public string DisplayName { get; }

    /// <summary>Grouping hint for the UI. See <see cref="MessageCategory"/>.</summary>
    public MessageCategory Category { get; }

    /// <summary>The CLR type of the message payload.</summary>
    public Type MessageType { get; }

    /// <summary>The Redis channel this message is published to unless overridden in the UI.</summary>
    public string DefaultChannel { get; }

    /// <summary>
    /// Builds a fresh, fully-populated example instance. Used to seed the JSON
    /// editor so the user starts from a realistic message rather than a blank.
    /// </summary>
    public Func<object> CreateTemplate { get; }

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"'{name}' must not be null or blank.", name)
            : value;
}
