using Pulsar.Core;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Xunit;

namespace Pulsar.Tests;

public class MessageTemplateServiceTests
{
    private static MessageDescriptorAccess Setup()
    {
        var host = TestSupport.LoadedHost();
        return new MessageDescriptorAccess(host, new MessageTemplateService());
    }

    private sealed record MessageDescriptorAccess(PluginHost Host, MessageTemplateService Templates);

    [Fact]
    public void Creates_template_json_from_descriptor_defaults()
    {
        var (host, templates) = Setup();
        var descriptor = host.Current!.FindMessage("HeartbeatTelemetry")!;

        var json = templates.CreateTemplateJson(descriptor);

        Assert.Contains("deviceId", json);
        Assert.Contains("device-001", json);
    }

    [Fact]
    public void Rehydrates_edited_json_back_to_message_instance()
    {
        var (host, templates) = Setup();
        var descriptor = host.Current!.FindMessage("HeartbeatTelemetry")!;
        var edited = templates.CreateTemplateJson(descriptor).Replace("device-001", "device-XYZ");

        var instance = templates.Rehydrate(edited, descriptor);

        Assert.Equal(descriptor.MessageType, instance.GetType());
        Assert.Equal("device-XYZ", instance.GetType().GetProperty("DeviceId")!.GetValue(instance));
    }

    [Fact]
    public void Invalid_json_throws_MessageEditException()
    {
        var (host, templates) = Setup();
        var descriptor = host.Current!.FindMessage("HeartbeatTelemetry")!;

        Assert.Throws<MessageEditException>(() => templates.Rehydrate("{ not valid", descriptor));
        Assert.Throws<MessageEditException>(() => templates.Rehydrate("", descriptor));
    }
}
