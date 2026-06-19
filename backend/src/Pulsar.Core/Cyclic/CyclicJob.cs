using Microsoft.Extensions.Logging;
using Pulsar.Contracts;
using Pulsar.Core.Activity;
using Pulsar.Core.Plugins;
using Pulsar.Core.Publishing;
using Pulsar.Core.Transport;

namespace Pulsar.Core.Cyclic;

/// <summary>
/// One running cyclic publisher: it runs its message's adapter over the edited JSON
/// and publishes on a fixed cadence until stopped. The adapter is invoked afresh
/// every tick, so adapters that stamp a timestamp/correlation id (e.g. the envelope)
/// produce a distinct message each time. A failing tick is recorded but does not stop
/// the loop — handy for watching recovery when the transport comes back.
/// </summary>
internal sealed class CyclicJob
{
    private readonly CatalogEntry _entry;
    private readonly string _payloadJson;
    private readonly MessageContext _context;
    private readonly IMessageTransport _transport;
    private readonly IActivityNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private long _published;
    private long _failures;
    private volatile string? _lastError;
    private long _lastPublishedAtTicks;
    private long _stoppedAtTicks;
    private Task _runner = Task.CompletedTask;

    public CyclicJob(
        Guid id,
        CatalogEntry entry,
        string channel,
        int intervalMs,
        string payloadJson,
        IMessageTransport transport,
        IActivityNotifier notifier,
        TimeProvider clock,
        ILogger logger)
    {
        Id = id;
        Channel = channel;
        IntervalMs = intervalMs;
        StartedAt = clock.GetUtcNow();
        _entry = entry;
        _payloadJson = payloadJson;
        _context = new MessageContext(entry.Key, channel, entry.Category);
        _transport = transport;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public Guid Id { get; }
    public string Channel { get; }
    public int IntervalMs { get; }
    public DateTimeOffset StartedAt { get; }

    public void Start() => _runner = Task.Run(() => RunAsync(_cts.Token));

    public void Stop()
    {
        if (_cts.IsCancellationRequested) return;
        Interlocked.CompareExchange(ref _stoppedAtTicks, _clock.GetUtcNow().UtcTicks, 0);
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    public async Task StopAndWaitAsync()
    {
        Stop();
        try { await _runner.ConfigureAwait(false); } catch { /* loop swallows its own errors */ }
        _cts.Dispose();
    }

    public CyclicJobInfo Snapshot()
    {
        var stoppedTicks = Interlocked.Read(ref _stoppedAtTicks);
        var lastTicks = Interlocked.Read(ref _lastPublishedAtTicks);
        return new CyclicJobInfo(
            Id,
            _entry.Key,
            _entry.DisplayName,
            Channel,
            IntervalMs,
            // Derive State from the same field as StoppedAt so a snapshot can never
            // report Running with a non-null StoppedAt (or vice versa).
            stoppedTicks == 0 ? CyclicJobState.Running : CyclicJobState.Stopped,
            Interlocked.Read(ref _published),
            Interlocked.Read(ref _failures),
            _lastError,
            StartedAt,
            stoppedTicks == 0 ? null : new DateTimeOffset(stoppedTicks, TimeSpan.Zero),
            lastTicks == 0 ? null : new DateTimeOffset(lastTicks, TimeSpan.Zero));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await PublishTickAsync(ct).ConfigureAwait(false);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(IntervalMs));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await PublishTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected when the job is stopped
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cyclic job {JobId} terminated unexpectedly.", Id);
        }
    }

    private async Task PublishTickAsync(CancellationToken ct)
    {
        var timestamp = _clock.GetUtcNow();
        try
        {
            var bytes = MessagePublishService.Invoke(_entry, _payloadJson, _context);
            await _transport.PublishAsync(Channel, bytes, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _published);
            Interlocked.Exchange(ref _lastPublishedAtTicks, timestamp.UtcTicks);
            _lastError = null;
            await _notifier.PublishedAsync(Activity(bytes.Length, true, null, timestamp)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failures);
            _lastError = ex.Message;
            await _notifier.PublishedAsync(Activity(0, false, ex.Message, timestamp)).ConfigureAwait(false);
        }
    }

    private PublishActivity Activity(int bytes, bool ok, string? error, DateTimeOffset ts) =>
        new("cyclic", _entry.Key, _entry.DisplayName, Channel, bytes, ok, error, ts, Id);
}
