using Pulsar.Core.Activity;
using Pulsar.Core.Cyclic;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;
using Pulsar.SampleMessages;

namespace Pulsar.Tests;

internal static class TestSupport
{
    /// <summary>Path to the built sample plugin assembly.</summary>
    public static string SamplePluginPath => typeof(SamplePlugin).Assembly.Location;

    public static PluginHost LoadedHost()
    {
        var host = new PluginHost(new PluginLoader());
        host.Load(SamplePluginPath);
        return host;
    }

    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
    }
}

/// <summary>In-memory transport that records every publish, with an optional failure switch.</summary>
internal sealed class FakeMessageTransport : IMessageTransport
{
    private readonly List<(string Channel, byte[] Payload)> _published = new();

    public TransportStatus Status { get; private set; } = TransportStatus.Connected("fake");
    public bool FailNextPublish { get; set; }

    public IReadOnlyList<(string Channel, byte[] Payload)> Published
    {
        get { lock (_published) return _published.ToList(); }
    }

    public int Count
    {
        get { lock (_published) return _published.Count; }
    }

    public Task<TransportStatus> ConnectAsync(string connectionString, CancellationToken ct = default)
    {
        Status = TransportStatus.Connected(connectionString);
        return Task.FromResult(Status);
    }

    public Task PublishAsync(string channel, byte[] payload, CancellationToken ct = default)
    {
        if (FailNextPublish)
        {
            FailNextPublish = false;
            throw new InvalidOperationException("Simulated transport failure.");
        }
        lock (_published) _published.Add((channel, payload));
        return Task.CompletedTask;
    }
}

/// <summary>Captures notifications raised by the domain.</summary>
internal sealed class RecordingNotifier : IActivityNotifier
{
    private readonly List<PublishActivity> _activities = new();
    private readonly List<CyclicJobInfo> _jobChanges = new();

    public IReadOnlyList<PublishActivity> Activities
    {
        get { lock (_activities) return _activities.ToList(); }
    }

    public IReadOnlyList<CyclicJobInfo> JobChanges
    {
        get { lock (_jobChanges) return _jobChanges.ToList(); }
    }

    public Task PublishedAsync(PublishActivity activity)
    {
        lock (_activities) _activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task JobChangedAsync(CyclicJobInfo job)
    {
        lock (_jobChanges) _jobChanges.Add(job);
        return Task.CompletedTask;
    }
}
