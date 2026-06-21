using System.CommandLine;
using System.CommandLine.Invocation;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Authoring;
using Pulsar.Cli.Rendering;
using Pulsar.Core.Adapters;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar new &lt;name&gt;</c> — scaffold a data-only plugin (manifest + a starter
/// schema and example per message), then load it back to prove it's valid.
/// </summary>
public sealed class NewCommand : ICliCommand
{
    private readonly PluginWriter _writer;

    public NewCommand(PluginWriter writer) => _writer = writer;

    public Command Build()
    {
        var nameArg = new Argument<string>("name", "Display name for the new plugin.");
        var outputOpt = new Option<string?>(new[] { "--output", "-o" }, "Output directory (default: ./<name>).");
        var adapterOpt = new Option<string>("--adapter", () => BuiltInAdapters.Passthrough,
            "Wire adapter for the plugin.").FromAmong(BuiltInAdapters.Names.ToArray());
        var messageOpt = new Option<string[]>("--message", "A message key to scaffold (repeatable).")
            { AllowMultipleArgumentsPerToken = true };
        var forceOpt = new Option<bool>("--force", "Overwrite an existing plugin at the output path.");

        var command = new Command("new", "Scaffold a new data-only plugin (manifest + schemas + examples).")
            { nameArg, outputOpt, adapterOpt, messageOpt, forceOpt };

        command.SetHandler((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArg);
            var output = ctx.ParseResult.GetValueForOption(outputOpt);
            var adapter = ctx.ParseResult.GetValueForOption(adapterOpt)!;
            var keys = ctx.ParseResult.GetValueForOption(messageOpt) ?? Array.Empty<string>();
            var force = ctx.ParseResult.GetValueForOption(forceOpt);

            var dir = string.IsNullOrWhiteSpace(output)
                ? Path.Combine(Directory.GetCurrentDirectory(), Naming.Slug(name))
                : output!;

            var messageKeys = keys.Length > 0 ? keys : new[] { "ExampleMessage" };
            var messages = messageKeys.Select(key =>
            {
                var category = Naming.Categorize(key);
                return new DraftMessage(key, key, category, Naming.DefaultChannel(key, category), StarterSchema(key));
            }).ToList();

            var result = _writer.Write(new PluginDraft(name, adapter, messages), dir, overwrite: force);

            ConsoleReport.Line(ctx.Console, $"Created plugin '{result.Catalog.Name}' with {result.Catalog.Messages.Count} message(s):");
            ConsoleReport.Line(ctx.Console, $"  {result.ManifestPath}");
            foreach (var message in result.Catalog.Messages)
                ConsoleReport.Line(ctx.Console, $"  - {message.Key} -> {message.DefaultChannel}");

            ctx.ExitCode = 0;
        });

        return command;
    }

    private static string StarterSchema(string key) => $$"""
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "{{key}}",
          "type": "object",
          "properties": {
            "id": { "type": "string", "minLength": 1, "default": "id-001" },
            "value": { "type": "number", "default": 0 }
          },
          "required": ["id"]
        }
        """;
}
