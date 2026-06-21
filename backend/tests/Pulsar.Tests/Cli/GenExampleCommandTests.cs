using Pulsar.Cli.Commands;
using Xunit;

namespace Pulsar.Tests.Cli;

public class GenExampleCommandTests
{
    [Fact]
    public async Task Generates_example_from_a_schema_file()
    {
        var schema = Path.Combine(CliTestSupport.SampleSchemaDir, "heartbeat.schema.json");

        var result = await CliTestSupport.RunAsync(new GenExampleCommand(), schema);

        Assert.Equal(0, result.Exit);
        Assert.Contains("device-001", result.Out); // the schema's default seeds the example
    }

    [Fact]
    public async Task Honours_a_json_pointer_into_a_sub_schema()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var path = Path.Combine(dir, "s.json");
            File.WriteAllText(path,
                "{\"$defs\":{\"Inner\":{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"string\",\"default\":\"hi\"}}}}}");

            var result = await CliTestSupport.RunAsync(new GenExampleCommand(), path, "--pointer", "#/$defs/Inner");

            Assert.Equal(0, result.Exit);
            Assert.Contains("hi", result.Out);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }

    [Fact]
    public async Task Missing_schema_file_is_a_hard_error()
    {
        var result = await CliTestSupport.RunAsync(new GenExampleCommand(), "no-such-schema.json");

        Assert.Equal(1, result.Exit);
    }
}
