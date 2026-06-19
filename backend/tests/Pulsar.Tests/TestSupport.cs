using Pulsar.Core.Activity;
using Pulsar.Core.Cyclic;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;
using Pulsar.SampleMessages;

namespace Pulsar.Tests;

internal static class TestSupport
{
    /// <summary>Path to the built (legacy <c>IPulsarPlugin</c>) sample plugin assembly.</summary>
    public static string SamplePluginPath => typeof(SamplePlugin).Assembly.Location;

    /// <summary>
    /// Path to the data-only sample manifest, copied next to the sample assembly in
    /// the test output (see Pulsar.Tests.csproj).
    /// </summary>
    public static string SampleManifestPath =>
        Path.Combine(Path.GetDirectoryName(SamplePluginPath)!, "manifest", "pulsar.plugin.json");

    public static CatalogLoader CatalogLoader() =>
        new(new LegacyPluginLoader(), new ManifestPluginLoader());

    /// <summary>A host with the data-only manifest plugin loaded (the primary path).</summary>
    public static PluginHost LoadedHost() => HostLoadedFrom(SampleManifestPath);

    /// <summary>A host with the legacy compiled plugin loaded (the back-compat path).</summary>
    public static PluginHost LegacyLoadedHost() => HostLoadedFrom(SamplePluginPath);

    private static PluginHost HostLoadedFrom(string path)
    {
        var host = new PluginHost(CatalogLoader());
        host.Load(path);
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
