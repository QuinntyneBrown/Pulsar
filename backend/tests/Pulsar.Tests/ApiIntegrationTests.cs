using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Pulsar.Tests;

/// <summary>
/// Boots the real API in-memory. The sample plugin is auto-loaded, and Redis is
/// pointed at a dead port so publishing fails fast and deterministically.
/// </summary>
public sealed class PulsarAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Pulsar:PluginPath", TestSupport.SamplePluginPath);
        builder.UseSetting("Pulsar:RedisConnectionString", "localhost:6399,connectTimeout=300,abortConnect=false");
    }
}

public class ApiIntegrationTests : IClassFixture<PulsarAppFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ApiIntegrationTests(PulsarAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Plugin_is_auto_loaded()
    {
        var doc = await GetJson("/api/plugin");
        Assert.True(doc.RootElement.GetProperty("isLoaded").GetBoolean());
        Assert.Equal("Sample Messages", doc.RootElement.GetProperty("plugin").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Messages_endpoint_lists_catalog()
    {
        var doc = await GetJson("/api/messages");
        Assert.Equal(6, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Message_detail_includes_editable_template()
    {
        var doc = await GetJson("/api/messages/HeartbeatTelemetry");
        var template = doc.RootElement.GetProperty("templateJson").GetString();
        Assert.NotNull(template);
        Assert.Contains("deviceId", template);
    }

    [Fact]
    public async Task Unknown_message_returns_404()
    {
        var response = await _client.GetAsync("/api/messages/nope");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cyclic_with_invalid_interval_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/cyclic",
            new { key = "HeartbeatTelemetry", channel = (string?)null, intervalMs = 1, payloadJson = "{}" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publish_with_redis_down_returns_502()
    {
        var detail = await GetJson("/api/messages/HeartbeatTelemetry");
        var template = detail.RootElement.GetProperty("templateJson").GetString();

        var response = await _client.PostAsJsonAsync("/api/publish",
            new { key = "HeartbeatTelemetry", channel = (string?)null, payloadJson = template });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private async Task<JsonDocument> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
