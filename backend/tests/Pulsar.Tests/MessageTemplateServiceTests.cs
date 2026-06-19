using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Xunit;

namespace Pulsar.Tests;

public class MessageTemplateServiceTests
{
    private static (PluginHost Host, MessageTemplateService Templates) Setup()
    {
        var host = TestSupport.LoadedHost();
        return (host, new MessageTemplateService());
    }

    [Fact]
    public void Creates_template_json_from_manifest_example()
    {
        var (host, templates) = Setup();
        var entry = host.Current!.FindMessage("HeartbeatTelemetry")!;

        var json = templates.CreateTemplateJson(entry);

        Assert.Contains("deviceId", json);
        Assert.Contains("device-001", json);
    }

    [Fact]
    public void Validate_passes_for_the_unedited_template()
    {
        var (host, templates) = Setup();
        var entry = host.Current!.FindMessage("HeartbeatTelemetry")!;

        var result = templates.Validate(entry, templates.CreateTemplateJson(entry));

        Assert.True(result.Matches);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Validate_reports_mismatch_for_an_out_of_enum_value()
    {
        var (host, templates) = Setup();
        var entry = host.Current!.FindMessage("HeartbeatTelemetry")!;
        var edited = templates.CreateTemplateJson(entry).Replace("\"Nominal\"", "\"Exploding\"");

        var result = templates.Validate(entry, edited);

        Assert.False(result.Matches);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public void Validate_is_advisory_and_never_throws_on_garbage()
    {
        var (host, templates) = Setup();
        var entry = host.Current!.FindMessage("HeartbeatTelemetry")!;

        var result = templates.Validate(entry, "{ not valid json");

        Assert.False(result.Matches); // reported, not thrown
    }
}
