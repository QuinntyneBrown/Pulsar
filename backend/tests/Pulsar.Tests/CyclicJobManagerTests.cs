using Pulsar.Core.Cyclic;
using Pulsar.Core.Messages;
using Xunit;

namespace Pulsar.Tests;

public class CyclicJobManagerTests
{
    private static (CyclicJobManager Manager, FakeMessageTransport Transport, string TemplateJson) Build()
    {
        var host = TestSupport.LoadedHost();
        var transport = new FakeMessageTransport();
        var templates = new MessageTemplateService();
        var manager = new CyclicJobManager(host, transport, templates, new RecordingNotifier());
        var json = templates.CreateTemplateJson(host.Current!.FindMessage("HeartbeatTelemetry")!);
        return (manager, transport, json);
    }

    [Fact]
    public async Task Start_ticks_until_stopped()
    {
        var (manager, transport, json) = Build();

        var info = manager.Start(new StartCyclicJobRequest("HeartbeatTelemetry", null, 10, json));
        Assert.Equal(CyclicJobState.Running, info.State);

        await TestSupport.WaitUntilAsync(() => manager.Get(info.Id)!.PublishedCount >= 2);

        var stopped = manager.Stop(info.Id);
        Assert.NotNull(stopped);
        Assert.Equal(CyclicJobState.Stopped, stopped!.State);
        Assert.True(transport.Count >= 2);
        Assert.Contains(manager.List(), j => j.Id == info.Id);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task Remove_stops_and_deletes_job()
    {
        var (manager, _, json) = Build();
        var info = manager.Start(new StartCyclicJobRequest("HeartbeatTelemetry", null, 10, json));

        Assert.True(manager.Remove(info.Id));
        Assert.Null(manager.Get(info.Id));

        await manager.DisposeAsync();
    }

    [Fact]
    public void Interval_below_minimum_is_rejected()
    {
        var (manager, _, json) = Build();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.Start(new StartCyclicJobRequest("HeartbeatTelemetry", null, 1, json)));
    }
}
