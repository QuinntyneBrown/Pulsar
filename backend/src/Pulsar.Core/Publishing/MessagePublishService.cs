using Pulsar.Contracts;
using Pulsar.Core.Activity;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;

namespace Pulsar.Core.Publishing;

/// <summary>Outcome of a single publish.</summary>
public sealed record PublishResult(string Channel, int ByteCount, DateTimeOffset Timestamp);

/// <summary>
/// Publishes a single message on demand — the path behind "send this event /
/// inject this fault now". Resolves the descriptor, rehydrates the payload,
/// serializes via the plugin, publishes via the transport, and reports activity.
/// </summary>
public sealed class MessagePublishService
{
    private readonly IPluginHost _host;
    private readonly IMessageTransport _transport;
    private readonly MessageTemplateService _templates;
    private readonly IActivityNotifier _notifier;
    private readonly TimeProvider _clock;

    public MessagePublishService(
        IPluginHost host,
        IMessageTransport transport,
        MessageTemplateService templates,
        IActivityNotifier notifier,
        TimeProvider? clock = null)
    {
        _host = host;
        _transport = transport;
        _templates = templates;
        _notifier = notifier;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PublishResult> PublishAsync(string key, string? channelOverride, string payloadJson, CancellationToken ct = default)
    {
        var plugin = _host.Current ?? throw new NoPluginLoadedException();
        var descriptor = plugin.FindMessage(key) ?? throw new MessageNotFoundException(key);
        var channel = ResolveChannel(descriptor, channelOverride);

        var payload = _templates.Rehydrate(payloadJson, descriptor);
        var bytes = Serialize(plugin.Serializer, payload, descriptor);
        var timestamp = _clock.GetUtcNow();

        try
        {
            await _transport.PublishAsync(channel, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _notifier.PublishedAsync(Activity(descriptor, channel, 0, false, ex.Message, timestamp)).ConfigureAwait(false);
            throw new PublishFailedException(channel, ex);
        }

        await _notifier.PublishedAsync(Activity(descriptor, channel, bytes.Length, true, null, timestamp)).ConfigureAwait(false);
        return new PublishResult(channel, bytes.Length, timestamp);
    }

    internal static string ResolveChannel(MessageDescriptor descriptor, string? channelOverride) =>
        string.IsNullOrWhiteSpace(channelOverride) ? descriptor.DefaultChannel : channelOverride.Trim();

    internal static byte[] Serialize(IMessageSerializer serializer, object payload, MessageDescriptor descriptor)
    {
        try
        {
            return serializer.Serialize(payload, descriptor)
                ?? throw new SerializationFailedException(descriptor.Key, new InvalidOperationException("Serializer returned null."));
        }
        catch (Exception ex) when (ex is not SerializationFailedException)
        {
            throw new SerializationFailedException(descriptor.Key, ex);
        }
    }

    private static PublishActivity Activity(MessageDescriptor d, string channel, int bytes, bool ok, string? error, DateTimeOffset ts) =>
        new("manual", d.Key, d.DisplayName, channel, bytes, ok, error, ts, JobId: null);
}
