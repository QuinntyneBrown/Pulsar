using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Activity;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;

namespace Pulsar.Core.Cyclic;

/// <summary>Request to start a cyclic publishing job.</summary>
public sealed record StartCyclicJobRequest(string MessageKey, string? Channel, int IntervalMs, string PayloadJson);

/// <summary>
/// Owns the set of running cyclic jobs. Starting validates against the loaded
/// catalog and captures the edited JSON; the resulting job then runs the message's
/// adapter on its own cadence. Stopped jobs are kept (with their counters) until
/// removed, so the UI can show what ran.
/// </summary>
public sealed class CyclicJobManager : IAsyncDisposable
{
    public const int MinIntervalMs = 10;
    public const int MaxIntervalMs = 60 * 60 * 1000; // one hour

    private readonly ConcurrentDictionary<Guid, CyclicJob> _jobs = new();
    private readonly object _gate = new();
    private bool _stopping;
    private readonly IPluginHost _host;
    private readonly IMessageTransport _transport;
    private readonly IActivityNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly ILoggerFactory _loggerFactory;

    public CyclicJobManager(
        IPluginHost host,
        IMessageTransport transport,
        IActivityNotifier notifier,
        TimeProvider? clock = null,
        ILoggerFactory? loggerFactory = null)
    {
        _host = host;
        _transport = transport;
        _notifier = notifier;
        _clock = clock ?? TimeProvider.System;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public CyclicJobInfo Start(StartCyclicJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IntervalMs is < MinIntervalMs or > MaxIntervalMs)
            throw new ArgumentOutOfRangeException(nameof(request),
                $"Interval must be between {MinIntervalMs} ms and {MaxIntervalMs} ms.");

        var catalog = _host.Current ?? throw new NoPluginLoadedException();
        var entry = catalog.FindMessage(request.MessageKey) ?? throw new MessageNotFoundException(request.MessageKey);
        var channel = Publishing.MessagePublishService.ResolveChannel(entry, request.Channel);

        var job = new CyclicJob(
            Guid.NewGuid(), entry, channel, request.IntervalMs, request.PayloadJson,
            _transport, _notifier, _clock,
            _loggerFactory.CreateLogger<CyclicJob>());

        // Register and start under the gate so a concurrent ClearAllAsync (plugin
        // reload) can never drop this job without stopping it — which would leave
        // it ticking against an adapter from an unloaded plugin's context.
        lock (_gate)
        {
            if (_stopping)
                throw new InvalidOperationException("A plugin reload is in progress. Please retry.");
            _jobs[job.Id] = job;
            job.Start();
        }

        var info = job.Snapshot();
        _ = _notifier.JobChangedAsync(info);
        return info;
    }

    public CyclicJobInfo? Stop(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var job)) return null;
        job.Stop();
        var info = job.Snapshot();
        _ = _notifier.JobChangedAsync(info);
        return info;
    }

    public bool Remove(Guid id)
    {
        if (!_jobs.TryRemove(id, out var job)) return false;
        _ = job.StopAndWaitAsync();
        // No notification: the job is gone, so re-broadcasting it would only cause
        // listeners to re-add it. The caller updates its own view.
        return true;
    }

    public IReadOnlyList<CyclicJobInfo> List() =>
        _jobs.Values.Select(j => j.Snapshot()).OrderBy(j => j.StartedAt).ToList();

    public CyclicJobInfo? Get(Guid id) => _jobs.TryGetValue(id, out var job) ? job.Snapshot() : null;

    /// <summary>Stops and removes every job. Used when the plugin is reloaded.</summary>
    public async Task ClearAllAsync()
    {
        List<CyclicJob> jobs;
        lock (_gate)
        {
            _stopping = true;
            jobs = _jobs.Values.ToList();
            _jobs.Clear();
        }

        try
        {
            await Task.WhenAll(jobs.Select(j => j.StopAndWaitAsync())).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate) _stopping = false;
        }
    }

    public async ValueTask DisposeAsync() => await ClearAllAsync().ConfigureAwait(false);
}
