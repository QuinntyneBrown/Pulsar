using Pulsar.Contracts;

namespace Pulsar.SampleMessages;

/// <summary>
/// Marks a class as a publishable message and records its default channel and
/// category. The plugin scans for this attribute to build its catalog, so adding
/// a new message type is just: write the POCO, annotate it, done.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PublishChannelAttribute : Attribute
{
    public PublishChannelAttribute(string channel, MessageCategory category)
    {
        Channel = channel;
        Category = category;
    }

    /// <summary>Default Redis channel for this message.</summary>
    public string Channel { get; }

    /// <summary>UI grouping category.</summary>
    public MessageCategory Category { get; }

    /// <summary>Stable key; defaults to the type name when not set.</summary>
    public string? Key { get; set; }

    /// <summary>Friendly display name; defaults to a spaced version of the type name.</summary>
    public string? DisplayName { get; set; }
}
