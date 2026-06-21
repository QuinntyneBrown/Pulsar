using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Authoring;
using Pulsar.Cli.Authoring.Importers;
using Pulsar.Cli.Commands;
using Pulsar.Core;
using Pulsar.Redis;

// ---- composition root --------------------------------------------------------
// A thin host wires configuration, logging, DI, and the existing Pulsar.Core/Redis
// services; the root command is assembled by discovering every ICliCommand (OCP).

var builder = Host.CreateApplicationBuilder(args);

// Logs go to stderr so stdout carries only command output (JSON, tables) — keeps the
// CLI pipe-friendly. --verbose raises the floor from Warning to Information.
var verbose = args.Contains("--verbose") || args.Contains("-v");
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(verbose ? LogLevel.Information : LogLevel.Warning);

builder.Services.Configure<PulsarOptions>(builder.Configuration.GetSection(PulsarOptions.SectionName));
builder.Services.AddPulsarCore();
builder.Services.AddPulsarRedis();

// Authoring services.
builder.Services.AddSingleton<PluginWriter>();
builder.Services.AddSingleton<ISpecImporter, JsonSchemaImporter>();
builder.Services.AddSingleton<ISpecImporter, OpenApiImporter>();
builder.Services.AddSingleton<ISpecImporter, AsyncApiImporter>();

// One registration per verb (the only edit needed to add a command).
builder.Services.AddSingleton<ICliCommand, ValidateCommand>();
builder.Services.AddSingleton<ICliCommand, ListCommand>();
builder.Services.AddSingleton<ICliCommand, GenExampleCommand>();
builder.Services.AddSingleton<ICliCommand, NewCommand>();
builder.Services.AddSingleton<ICliCommand, PublishCommand>();
builder.Services.AddSingleton<ICliCommand, ImportCommand>();

using var host = builder.Build();

var root = new RootCommand("Pulsar — author, validate, generate, and publish data-only message plugins.");
root.AddGlobalOption(new Option<bool>(new[] { "--verbose", "-v" }, "Show informational logs on stderr."));
foreach (var command in host.Services.GetServices<ICliCommand>())
    root.AddCommand(command.Build());

// Turn expected Pulsar errors into a friendly stderr line + exit 1, instead of a stack
// trace. Added after UseDefaults so it wraps the command invocation (inner middleware).
var parser = new CommandLineBuilder(root)
    .UseDefaults()
    .AddMiddleware(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (PulsarException ex)
        {
            context.Console.Error.WriteLine($"error: {ex.Message}");
            context.ExitCode = 1;
        }
        catch (Exception ex)
        {
            context.Console.Error.WriteLine($"error: {ex.Message}");
            context.ExitCode = 1;
        }
    })
    .Build();

return await parser.InvokeAsync(args);
