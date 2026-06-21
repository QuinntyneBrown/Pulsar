using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Rendering;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar validate &lt;manifest|folder&gt;</c> — load the catalog and check each
/// message's example against its schema. Exit codes are CI-friendly:
/// <c>0</c> clean, <c>1</c> hard load error (via the exception middleware),
/// <c>2</c> advisory mismatch (raised to <c>1</c> with <c>--strict</c>, dropped to
/// <c>0</c> with <c>--warn-only</c>).
/// </summary>
public sealed class ValidateCommand : ICliCommand
{
    private readonly CatalogLoader _loader;

    public ValidateCommand(CatalogLoader loader) => _loader = loader;

    public Command Build()
    {
        var pathArg = new Argument<string>("path", "A manifest (.json), a plugin folder, or a legacy plugin (.dll).");
        var strictOpt = new Option<bool>("--strict", "Treat example/schema mismatches as failures (exit 1).");
        var warnOnlyOpt = new Option<bool>("--warn-only", "Never fail on example/schema mismatches (exit 0).");
        var command = new Command("validate", "Validate a plugin and check each example against its schema.")
            { pathArg, strictOpt, warnOnlyOpt };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = PluginPaths.Resolve(ctx.ParseResult.GetValueForArgument(pathArg));
            var strict = ctx.ParseResult.GetValueForOption(strictOpt);
            var warnOnly = ctx.ParseResult.GetValueForOption(warnOnlyOpt);
            ctx.ExitCode = Run(ctx.Console, path, strict, warnOnly);
        });

        return command;
    }

    private int Run(IConsole console, string path, bool strict, bool warnOnly)
    {
        using var catalog = _loader.Load(path); // hard errors surface as PulsarException -> middleware -> exit 1

        var rows = new List<string[]>(catalog.Messages.Count);
        var mismatches = 0;

        foreach (var message in catalog.Messages)
        {
            string status;
            if (message.Schema is null)
            {
                status = "no schema";
            }
            else
            {
                var result = SchemaValidator.Validate(message.CreateTemplateJson(), message.Schema);
                if (result.Matches)
                {
                    status = "ok";
                }
                else
                {
                    status = "MISMATCH";
                    mismatches++;
                    foreach (var msg in result.Messages)
                        ConsoleReport.Warn(console, $"{message.Key}: {msg}");
                }
            }

            rows.Add(new[] { message.Key, message.DefaultChannel, status });
        }

        ConsoleReport.Line(console, $"{catalog.Name}  ({catalog.Messages.Count} message(s))");
        ConsoleReport.Table(console, new[] { "KEY", "CHANNEL", "EXAMPLE" }, rows);
        ConsoleReport.Line(console);

        if (mismatches == 0)
        {
            ConsoleReport.Line(console, "OK — every example matches its schema.");
            return 0;
        }

        ConsoleReport.Line(console, $"{mismatches} message(s) have an example that does not match its schema.");
        if (warnOnly) return 0;
        return strict ? 1 : 2;
    }
}
