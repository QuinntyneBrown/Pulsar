using System.CommandLine;
using System.CommandLine.Invocation;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Rendering;
using Pulsar.Core.Plugins;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar list &lt;manifest|folder&gt;</c> — load a catalog and print its messages.
/// </summary>
public sealed class ListCommand : ICliCommand
{
    private readonly CatalogLoader _loader;

    public ListCommand(CatalogLoader loader) => _loader = loader;

    public Command Build()
    {
        var pathArg = new Argument<string>("path", "A manifest (.json) or plugin folder.");
        var command = new Command("list", "List the messages in a plugin catalog.") { pathArg };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = PluginPaths.Resolve(ctx.ParseResult.GetValueForArgument(pathArg));
            using var catalog = _loader.Load(path);

            ConsoleReport.Line(ctx.Console, $"{catalog.Name}  ({catalog.Messages.Count} message(s))  [{catalog.SourcePath}]");
            ConsoleReport.Line(ctx.Console);

            var rows = catalog.Messages
                .Select(m => new[] { m.Key, m.DisplayName, m.Category.ToString(), m.DefaultChannel, m.Schema is null ? "-" : "yes" })
                .ToList();
            ConsoleReport.Table(ctx.Console, new[] { "KEY", "DISPLAY NAME", "CATEGORY", "CHANNEL", "SCHEMA" }, rows);
            ctx.ExitCode = 0;
        });

        return command;
    }
}
