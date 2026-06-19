using Pulsar.Core;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Pulsar.Core.Publishing;
using Xunit;

namespace Pulsar.Tests;

public class MessagePublishServiceTests
{
    private static (MessagePublishService Service, FakeMessageTransport Transport, RecordingNotifier Notifier, string TemplateJson, MessageTemplateService Templates, PluginHost Host)
        Build()
    {
        var host = TestSupport.LoadedHost();
        var transport = new FakeMessageTransport();
        var notifier = new RecordingNotifier();
        var templates = new MessageTemplateService();
        var service = new MessagePublishService(host, transport, templates, notifier);
        var template = templates.CreateTemplateJson(host.Current!.FindMessage("HeartbeatTelemetry")!);
        return (service, transport, notifier, template, templates, host);
    }

    [Fact]
    public async Task Publishes_to_default_channel_and_reports_success()
    {
        var (service, transport, notifier, json, _, _) = Build();

        var result = await service.PublishAsync("HeartbeatTelemetry", channelOverride: null, json);

        Assert.Equal("telemetry.heartbeat", result.Channel);
        Assert.True(result.ByteCount > 0);
        Assert.Single(transport.Published);
        Assert.Equal("telemetry.heartbeat", transport.Published[0].Channel);
        Assert.Single(notifier.Activities);
        Assert.True(notifier.Activities[0].Success);
    }

    [Fact]
    public async Task Channel_override_is_honoured()
    {
        var (service, transport, _, json, _, _) = Build();

        await service.PublishAsync("HeartbeatTelemetry", "custom.channel", json);

        Assert.Equal("custom.channel", transport.Published[0].Channel);
    }

    [Fact]
    public async Task Transport_failure_surfaces_as_PublishFailedException_and_failure_activity()
    {
        var (service, transport, notifier, json, _, _) = Build();
        transport.FailNextPublish = true;

        await Assert.ThrowsAsync<PublishFailedException>(() =>
            service.PublishAsync("HeartbeatTelemetry", null, json));

        Assert.Contains(notifier.Activities, a => !a.Success);
    }

    [Fact]
    public async Task No_plugin_loaded_throws()
    {
        var service = new MessagePublishService(
            new PluginHost(new PluginLoader()), new FakeMessageTransport(), new MessageTemplateService(), new RecordingNotifier());

        await Assert.ThrowsAsync<NoPluginLoadedException>(() =>
            service.PublishAsync("HeartbeatTelemetry", null, "{}"));
    }

    [Fact]
    public async Task Unknown_message_key_throws()
    {
        var (service, _, _, json, _, _) = Build();

        await Assert.ThrowsAsync<MessageNotFoundException>(() =>
            service.PublishAsync("NotARealMessage", null, json));
    }
}
