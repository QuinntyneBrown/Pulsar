namespace Pulsar.Contracts;

/// <summary>
/// The single entry point Pulsar looks for in a plugin assembly. Implement this
/// with a public, parameterless constructor; Pulsar discovers it by reflection
/// when you load the assembly, so there is no configuration to wire up.
/// </summary>
/// <remarks>
/// A plugin bundles together <em>what</em> can be sent (the
/// <see cref="Catalog"/>) and <em>how</em> it is put on the wire (the
/// <see cref="Serializer"/>). To target a different system under test, build a
/// different plugin — Pulsar itself never changes.
/// </remarks>
public interface IPulsarPlugin
{
    /// <summary>Display name for the plugin, shown in the UI.</summary>
    string Name { get; }

    /// <summary>The messages this plugin can produce.</summary>
    IMessageCatalog Catalog { get; }

    /// <summary>The serializer that defines the wire format for this plugin's messages.</summary>
    IMessageSerializer Serializer { get; }
}
