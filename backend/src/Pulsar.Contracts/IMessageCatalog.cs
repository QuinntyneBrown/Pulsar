namespace Pulsar.Contracts;

/// <summary>
/// The set of messages a plugin can produce. Pulsar reads this once when the
/// plugin is loaded and presents the messages in the UI.
/// </summary>
public interface IMessageCatalog
{
    /// <summary>All messages this plugin exposes.</summary>
    IReadOnlyList<MessageDescriptor> Messages { get; }
}
