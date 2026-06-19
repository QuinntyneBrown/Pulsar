using Pulsar.Contracts;
using Pulsar.Core.Activity;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;

namespace Pulsar.Core.Publishing;

/// <summary>Outcome of a single publish.</summary>
public sealed record PublishResult(string Channel, int ByteCount, DateTimeOffset Timestamp);

/// <summary>
/// Publishes a single message on demand — the path behind "send this event /
/// inject this fault now". Resolves the entry, runs its adapter over the edited
/// JSON, publishes the resulting bytes via the transport, and reports activity.
/// </summary>
public sealed class MessagePublishService
{
    private readonly IPluginHost _host;
    private readonly IMessageTransport _transport;
    private readonly IActivityNotifier _notifier;
    private readonly TimeProvider _clock;

    public MessagePublishService(
        IPluginHost host,
        IMessageTransport transport,
        IActivityNotifier notifier,
        TimeProvider? clock = null)
    {
        _host = host;
        _transport = transport;
        _notifier = notifier;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<PublishResult> PublishAsync(string key, string? channelOverride, string payloadJson, CancellationToken ct = default)
    {
        var catalog = _host.Current ?? throw new NoPluginLoadedException();
        var entry = catalog.FindMessage(key) ?? throw new MessageNotFoundException(key);
        var channel = ResolveChannel(entry, channelOverride);
        var context = new MessageContext(entry.Key, channel, entry.Category);

        var bytes = Invoke(entry, payloadJson, context);
        var timestamp = _clock.GetUtcNow();

        try
        {
            await _transport.PublishAsync(channel, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _notifier.PublishedAsync(Activity(entry, channel, 0, false, ex.Message, timestamp)).ConfigureAwait(false);
            throw new PublishFailedException(channel, ex);
        }

        await _notifier.PublishedAsync(Activity(entry, channel, bytes.Length, true, null, timestamp)).ConfigureAwait(false);
        return new PublishResult(channel, bytes.Length, timestamp);
    }

    internal static string ResolveChannel(CatalogEntry entry, string? channelOverride) =>
        string.IsNullOrWhiteSpace(channelOverride) ? entry.DefaultChannel : channelOverride.Trim();

    /// <summary>
    /// Runs an entry's adapter over the edited JSON, normalizing any failure into a
    /// <see cref="SerializationFailedException"/>. Shared by the one-shot and cyclic paths.
    /// </summary>
    internal static byte[] Invoke(CatalogEntry entry, string payloadJson, MessageContext context)
    {
        try
        {
            return entry.Adapter(payloadJson, context)
                ?? throw new SerializationFailedException(entry.Key, new InvalidOperationException("Adapter returned null."));
        }
        catch (Exception ex) when (ex is not SerializationFailedException)
        {
            throw new SerializationFailedException(entry.Key, ex);
        }
    }

    private static PublishActivity Activity(CatalogEntry e, string channel, int bytes, bool ok, string? error, DateTimeOffset ts) =>
        new("manual", e.Key, e.DisplayName, channel, bytes, ok, error, ts, JobId: null);
}
