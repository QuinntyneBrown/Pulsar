using System.CommandLine;
using System.CommandLine.Invocation;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Authoring;
using Pulsar.Cli.Authoring.Importers;
using Pulsar.Cli.Rendering;
using Pulsar.Core;
using Pulsar.Core.Adapters;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar import &lt;format&gt; &lt;spec&gt; -o &lt;dir&gt;</c> — generate a plugin from an
/// existing contract. Each registered <see cref="ISpecImporter"/> is exposed as a
/// subcommand named by its format, so adding a format needs no change here (OCP).
/// </summary>
public sealed class ImportCommand : ICliCommand
{
    private readonly IEnumerable<ISpecImporter> _importers;
    private readonly PluginWriter _writer;

    public ImportCommand(IEnumerable<ISpecImporter> importers, PluginWriter writer)
    {
        _importers = importers;
        _writer = writer;
    }

    public Command Build()
    {
        var command = new Command("import", "Generate a plugin from an existing contract (OpenAPI/AsyncAPI/JSON Schema).");
        foreach (var importer in _importers)
            command.AddCommand(BuildSubcommand(importer));
        return command;
    }

    private Command BuildSubcommand(ISpecImporter importer)
    {
        var specArg = new Argument<string>("spec", "Path to the source document.");
        var outputOpt = new Option<string?>(new[] { "--output", "-o" }, "Output directory for the generated plugin.")
            { IsRequired = true };
        var nameOpt = new Option<string?>("--name", "Plugin display name (default: the output folder name).");
        var adapterOpt = new Option<string>("--adapter", () => BuiltInAdapters.Passthrough,
            "Wire adapter for the plugin.").FromAmong(BuiltInAdapters.Names.ToArray());
        var forceOpt = new Option<bool>("--force", "Overwrite an existing plugin at the output path.");

        var subcommand = new Command(importer.Format, importer.Description)
            { specArg, outputOpt, nameOpt, adapterOpt, forceOpt };

        subcommand.SetHandler((InvocationContext ctx) =>
        {
            var spec = ctx.ParseResult.GetValueForArgument(specArg);
            var output = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var name = ctx.ParseResult.GetValueForOption(nameOpt);
            var adapter = ctx.ParseResult.GetValueForOption(adapterOpt)!;
            var force = ctx.ParseResult.GetValueForOption(forceOpt);

            if (!File.Exists(spec))
                throw new PluginLoadException($"Spec file not found: {spec}");

            var pluginName = string.IsNullOrWhiteSpace(name)
                ? Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(output)))
                : name!;

            PluginDraft draft;
            using (var stream = File.OpenRead(spec))
                draft = importer.Import(stream, new ImportOptions(pluginName, adapter));

            var result = _writer.Write(draft, output, overwrite: force);

            ConsoleReport.Line(ctx.Console, $"Imported {result.Catalog.Messages.Count} message(s) from {importer.Format} into:");
            ConsoleReport.Line(ctx.Console, $"  {result.ManifestPath}");
            foreach (var message in result.Catalog.Messages)
                ConsoleReport.Line(ctx.Console, $"  - {message.Key} -> {message.DefaultChannel}");

            ctx.ExitCode = 0;
        });

        return subcommand;
    }
}
