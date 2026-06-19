using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Pulsar.Core.Activity;
using Pulsar.Core.Cyclic;

namespace Pulsar.Api.Activity;

/// <summary>
/// Fans domain activity out to every connected Server-Sent Events client. Each
/// browser stream subscribes for a bounded channel of pre-rendered SSE frames;
/// slow clients drop their oldest frames rather than blocking publishers.
/// </summary>
public sealed class ActivityBroadcaster : IActivityNotifier
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    /// <summary>Registers a new client stream. Dispose the result to unsubscribe.</summary>
    public IDisposable Subscribe(out ChannelReader<string> reader)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _subscribers[id] = channel;
        reader = channel.Reader;
        return new Subscription(() =>
        {
            if (_subscribers.TryRemove(id, out var ch))
                ch.Writer.TryComplete();
        });
    }

    public Task PublishedAsync(PublishActivity activity) => Broadcast("published", activity);

    public Task JobChangedAsync(CyclicJobInfo job) => Broadcast("jobChanged", job);

    private Task Broadcast(string eventName, object payload)
    {
        var frame = $"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, Json)}\n\n";
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(frame);
        return Task.CompletedTask;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        public Subscription(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}
