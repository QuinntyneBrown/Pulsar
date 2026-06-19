using Pulsar.Core.Messages;
using Xunit;

namespace Pulsar.Tests;

public class SchemaValidatorTests
{
    private const string Schema = """
    {
      "type": "object",
      "properties": {
        "name":  { "type": "string", "minLength": 1 },
        "count": { "type": "integer", "minimum": 0 },
        "level": { "type": "string", "enum": ["low", "high"] }
      },
      "required": ["name", "level"]
    }
    """;

    private static ValidationResult Validate(string payload) =>
        SchemaValidator.Validate(payload, CompiledSchema.Parse(Schema));

    [Fact]
    public void Valid_payload_matches()
    {
        var result = Validate("""{ "name": "ok", "count": 3, "level": "low" }""");
        Assert.True(result.Matches);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Missing_required_property_is_flagged()
    {
        var result = Validate("""{ "name": "ok" }""");
        Assert.False(result.Matches);
        Assert.Contains(result.Messages, m => m.Contains("level"));
    }

    [Fact]
    public void Type_mismatch_is_flagged()
    {
        var result = Validate("""{ "name": 5, "level": "low" }""");
        Assert.False(result.Matches);
        Assert.Contains(result.Messages, m => m.Contains("type"));
    }

    [Fact]
    public void Enum_and_minimum_violations_are_flagged()
    {
        var result = Validate("""{ "name": "ok", "count": -1, "level": "medium" }""");
        Assert.False(result.Matches);
        Assert.Contains(result.Messages, m => m.Contains("enum"));
        Assert.Contains(result.Messages, m => m.Contains("minimum"));
    }

    [Fact]
    public void Invalid_json_is_reported_not_thrown()
    {
        var result = Validate("{ not json");
        Assert.False(result.Matches);
        Assert.NotEmpty(result.Messages);
    }
}
