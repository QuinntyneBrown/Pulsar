namespace Pulsar.Core;

/// <summary>Base type for all expected, user-facing Pulsar errors.</summary>
public abstract class PulsarException : Exception
{
    protected PulsarException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A plugin manifest or adapter could not be loaded.</summary>
public sealed class PluginLoadException : PulsarException
{
    public PluginLoadException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>An operation requiring a loaded plugin was attempted while none is loaded.</summary>
public sealed class NoPluginLoadedException : PulsarException
{
    public NoPluginLoadedException() : base("No plugin is loaded. Load a message plugin first.") { }
}

/// <summary>The requested message key does not exist in the loaded plugin's catalog.</summary>
public sealed class MessageNotFoundException : PulsarException
{
    public MessageNotFoundException(string key) : base($"No message with key '{key}' exists in the loaded plugin.") { }
}

/// <summary>The payload JSON supplied by the user could not be processed.</summary>
public sealed class MessageEditException : PulsarException
{
    public MessageEditException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The plugin's adapter threw while converting a message for publishing.</summary>
public sealed class SerializationFailedException : PulsarException
{
    public SerializationFailedException(string key, Exception inner)
        : base($"The plugin adapter failed for message '{key}': {inner.Message}", inner) { }
}

/// <summary>Publishing to the transport (Redis) failed.</summary>
public sealed class PublishFailedException : PulsarException
{
    public PublishFailedException(string channel, Exception inner)
        : base($"Failed to publish to channel '{channel}': {inner.Message}", inner) { }
}
