using System.Text;
using System.Text.Json;
using Pulsar.Contracts;
using Pulsar.Core.Adapters;
using Xunit;

namespace Pulsar.Tests;

public class BuiltInAdaptersTests
{
    private static readonly MessageContext Ctx = new("OrderPlaced", "orders.placed", MessageCategory.Event);

    [Fact]
    public void Passthrough_publishes_bytes_verbatim()
    {
        const string json = """{"a":1,"b":"x"}""";
        var bytes = BuiltInAdapters.JsonPassthrough(json, Ctx);
        Assert.Equal(json, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Envelope_wraps_payload_with_key_and_metadata()
    {
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(
            BuiltInAdapters.JsonEnvelope("""{"id":42}""", Ctx)));
        var root = doc.RootElement;

        Assert.Equal("OrderPlaced", root.GetProperty("messageType").GetString());
        Assert.True(root.TryGetProperty("correlationId", out _));
        Assert.True(root.TryGetProperty("emittedAtUnixMs", out _));
        Assert.Equal(42, root.GetProperty("payload").GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData("json-passthrough", true)]
    [InlineData("json-envelope", true)]
    [InlineData("JSON-ENVELOPE", true)]   // case-insensitive
    [InlineData("nope", false)]
    [InlineData(null, false)]
    public void TryResolve_recognizes_built_in_names(string? name, bool expected)
    {
        Assert.Equal(expected, BuiltInAdapters.TryResolve(name, out _));
    }
}
