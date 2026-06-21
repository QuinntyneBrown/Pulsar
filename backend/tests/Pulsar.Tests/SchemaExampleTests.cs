using Pulsar.Core.Messages;
using Xunit;

namespace Pulsar.Tests;

public class SchemaExampleTests
{
    [Fact]
    public void Prefers_the_first_examples_entry()
    {
        var schema = CompiledSchema.Parse(
            "{\"examples\":[{\"a\":1}],\"properties\":{\"a\":{\"type\":\"integer\"}}}");

        Assert.Contains("\"a\": 1", SchemaExample.FromSchema(schema));
    }

    [Fact]
    public void Falls_back_to_object_default()
    {
        var schema = CompiledSchema.Parse(
            "{\"default\":{\"mode\":\"auto\"},\"type\":\"object\"}");

        Assert.Contains("\"mode\": \"auto\"", SchemaExample.FromSchema(schema));
    }

    [Fact]
    public void Builds_a_skeleton_from_property_types_when_no_example()
    {
        var schema = CompiledSchema.Parse(
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"count\":{\"type\":\"integer\"}}}");

        var example = SchemaExample.FromSchema(schema);

        Assert.Contains("\"name\"", example);
        Assert.Contains("\"count\"", example);
    }

    [Fact]
    public void Returns_empty_object_when_nothing_to_go_on()
    {
        var schema = CompiledSchema.Parse("{\"type\":\"string\"}");

        Assert.Equal("{}", SchemaExample.FromSchema(schema));
    }
}
