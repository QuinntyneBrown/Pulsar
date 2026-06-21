using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Rendering;
using Pulsar.Core;
using Pulsar.Core.Messages;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar gen-example &lt;schema.json&gt;</c> — emit a seed example payload from a
/// JSON Schema (the same logic the loader uses to seed the editor), optionally targeting
/// a sub-schema by JSON pointer.
/// </summary>
public sealed class GenExampleCommand : ICliCommand
{
    public Command Build()
    {
        var schemaArg = new Argument<string>("schema", "Path to a JSON Schema file.");
        var pointerOpt = new Option<string?>("--pointer", "JSON pointer to a sub-schema, e.g. #/$defs/Heartbeat.");
        var outputOpt = new Option<string?>(new[] { "--output", "-o" }, "Write the example to this file instead of stdout.");
        var command = new Command("gen-example", "Generate an example payload from a JSON Schema.")
            { schemaArg, pointerOpt, outputOpt };

        command.SetHandler((InvocationContext ctx) =>
        {
            var schemaPath = ctx.ParseResult.GetValueForArgument(schemaArg);
            var pointer = ctx.ParseResult.GetValueForOption(pointerOpt);
            var output = ctx.ParseResult.GetValueForOption(outputOpt);

            if (!File.Exists(schemaPath))
                throw new PluginLoadException($"Schema file not found: {schemaPath}");

            CompiledSchema schema;
            try
            {
                schema = CompiledSchema.ParseWithPointer(File.ReadAllText(schemaPath), pointer);
            }
            catch (JsonException ex)
            {
                throw new PluginLoadException($"Schema is not valid JSON or the pointer did not resolve: {ex.Message}", ex);
            }

            var example = SchemaExample.FromSchema(schema);
            if (string.IsNullOrWhiteSpace(output))
            {
                ConsoleReport.Line(ctx.Console, example);
            }
            else
            {
                File.WriteAllText(output, example);
                ConsoleReport.Error(ctx.Console, $"Wrote example to {Path.GetFullPath(output)}");
            }

            ctx.ExitCode = 0;
        });

        return command;
    }
}
