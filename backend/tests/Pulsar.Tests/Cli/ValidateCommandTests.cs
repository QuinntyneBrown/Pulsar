using Pulsar.Cli.Commands;
using Xunit;

namespace Pulsar.Tests.Cli;

public class ValidateCommandTests
{
    private static ValidateCommand Validate() => new(CliTestSupport.Loader());

    [Fact]
    public async Task Valid_sample_manifest_passes_with_exit_zero()
    {
        var result = await CliTestSupport.RunAsync(Validate(), TestSupport.SampleManifestPath);

        Assert.Equal(0, result.Exit);
        Assert.Contains("every example matches", result.Out);
    }

    [Fact]
    public async Task Folder_path_resolves_to_its_manifest()
    {
        var folder = Path.GetDirectoryName(TestSupport.SampleManifestPath)!;

        var result = await CliTestSupport.RunAsync(Validate(), folder);

        Assert.Equal(0, result.Exit);
    }

    [Fact]
    public async Task Missing_manifest_is_a_hard_error_exit_one()
    {
        var result = await CliTestSupport.RunAsync(Validate(), "does-not-exist.json");

        Assert.Equal(1, result.Exit);
    }

    [Theory]
    [InlineData(new string[0], 2)]              // default: advisory mismatch
    [InlineData(new[] { "--strict" }, 1)]       // strict: mismatch is a failure
    [InlineData(new[] { "--warn-only" }, 0)]    // warn-only: never fail
    public async Task Drifted_example_uses_the_configured_exit_code(string[] flags, int expected)
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            await CliTestSupport.RunAsync(new NewCommand(CliTestSupport.Writer()), "Drift", "-o", dir, "--message", "Foo");
            // Corrupt the example so it no longer satisfies the starter schema (missing required "id").
            File.WriteAllText(Path.Combine(dir, "examples", "foo.json"), "{ \"value\": \"not-a-number\" }");

            var args = new[] { dir }.Concat(flags).ToArray();
            var result = await CliTestSupport.RunAsync(Validate(), args);

            Assert.Equal(expected, result.Exit);
            Assert.Contains("MISMATCH", result.Out);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }
}
