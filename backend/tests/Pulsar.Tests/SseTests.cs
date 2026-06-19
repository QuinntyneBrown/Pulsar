using System.Text;
using Xunit;

namespace Pulsar.Tests;

public class SseTests : IClassFixture<PulsarAppFactory>
{
    private readonly PulsarAppFactory _factory;

    public SseTests(PulsarAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Activity_stream_is_a_live_event_stream()
    {
        var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await client.GetAsync(
            "/api/activity/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // The endpoint writes a ": connected" preamble as soon as the stream opens.
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[128];
        var read = await stream.ReadAsync(buffer, cts.Token);
        var text = Encoding.UTF8.GetString(buffer, 0, read);

        Assert.Contains(":", text);
    }
}
