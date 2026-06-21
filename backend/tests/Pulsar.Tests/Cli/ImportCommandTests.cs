using Pulsar.Cli.Authoring.Importers;
using Pulsar.Cli.Commands;
using Xunit;

namespace Pulsar.Tests.Cli;

public class ImportCommandTests
{
    private static ImportCommand Import() => new(
        new ISpecImporter[] { new JsonSchemaImporter(), new OpenApiImporter(), new AsyncApiImporter() },
        CliTestSupport.Writer());

    private static async Task AssertValidates(string folder)
    {
        var validated = await CliTestSupport.RunAsync(new ValidateCommand(CliTestSupport.Loader()), folder);
        Assert.Equal(0, validated.Exit);
    }

    [Fact]
    public async Task Imports_a_json_schema_as_one_message()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var spec = Path.Combine(dir, "schema.json");
            File.WriteAllText(spec, "{\"title\":\"Reading\",\"type\":\"object\",\"properties\":{\"v\":{\"type\":\"number\",\"default\":1}}}");
            var outDir = Path.Combine(dir, "out");

            var result = await CliTestSupport.RunAsync(Import(), "jsonschema", spec, "-o", outDir);

            Assert.Equal(0, result.Exit);
            await AssertValidates(outDir);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }

    [Fact]
    public async Task Imports_one_message_per_openapi_component_schema()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var spec = Path.Combine(dir, "openapi.json");
            File.WriteAllText(spec, """
                {
                  "openapi": "3.0.0",
                  "info": { "title": "Sensors", "version": "1.0.0" },
                  "paths": {},
                  "components": { "schemas": {
                    "TemperatureReading": { "type": "object", "properties": { "celsius": { "type": "number" } }, "required": ["celsius"] },
                    "OperatorAlert": { "type": "object", "properties": { "level": { "type": "string" } }, "required": ["level"] }
                  } }
                }
                """);
            var outDir = Path.Combine(dir, "out");

            var result = await CliTestSupport.RunAsync(Import(), "openapi", spec, "-o", outDir);

            Assert.Equal(0, result.Exit);
            Assert.Contains("2 message(s)", result.Out);
            await AssertValidates(outDir);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }

    [Fact]
    public async Task Imports_asyncapi_messages_and_resolves_channels()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var spec = Path.Combine(dir, "asyncapi.yaml");
            File.WriteAllText(spec, """
                asyncapi: '2.6.0'
                info:
                  title: Telemetry
                  version: '1.0.0'
                channels:
                  telemetry/heartbeat:
                    publish:
                      message:
                        $ref: '#/components/messages/Heartbeat'
                components:
                  messages:
                    Heartbeat:
                      title: Heartbeat
                      payload:
                        type: object
                        properties:
                          deviceId: { type: string }
                        required: [deviceId]
                """);
            var outDir = Path.Combine(dir, "out");

            var result = await CliTestSupport.RunAsync(Import(), "asyncapi", spec, "-o", outDir);

            Assert.Equal(0, result.Exit);
            Assert.Contains("telemetry/heartbeat", result.Out); // channel resolved from the channels map
            await AssertValidates(outDir);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }
}
