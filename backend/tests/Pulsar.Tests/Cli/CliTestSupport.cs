using System.CommandLine;
using System.CommandLine.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Authoring;
using Pulsar.Core.Plugins;

namespace Pulsar.Tests.Cli;

/// <summary>Captured outcome of running a CLI command against a <see cref="TestConsole"/>.</summary>
internal sealed record CliResult(int Exit, string Out, string Err);

internal static class CliTestSupport
{
    public static CatalogLoader Loader() => TestSupport.CatalogLoader();

    public static PluginWriter Writer() => new(Loader(), NullLogger<PluginWriter>.Instance);

    /// <summary>Builds the command and invokes it as a standalone root with the given args.</summary>
    public static async Task<CliResult> RunAsync(ICliCommand command, params string[] args)
    {
        var console = new TestConsole();
        var exit = await command.Build().InvokeAsync(args, console);
        return new CliResult(exit, console.Out.ToString() ?? "", console.Error.ToString() ?? "");
    }

    public static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pulsar-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Delete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>The folder containing the sample manifest's schema files.</summary>
    public static string SampleSchemaDir =>
        Path.Combine(Path.GetDirectoryName(TestSupport.SampleManifestPath)!, "schemas");
}
