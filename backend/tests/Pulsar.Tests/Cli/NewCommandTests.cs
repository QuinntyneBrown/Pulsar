using Pulsar.Cli.Commands;
using Xunit;

namespace Pulsar.Tests.Cli;

public class NewCommandTests
{
    [Fact]
    public async Task Scaffolds_a_plugin_that_then_validates_clean()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var created = await CliTestSupport.RunAsync(
                new NewCommand(CliTestSupport.Writer()),
                "My Plugin", "-o", dir, "--message", "Alpha", "--message", "Beta", "--adapter", "json-envelope");

            Assert.Equal(0, created.Exit);
            Assert.True(File.Exists(Path.Combine(dir, "pulsar.plugin.json")));
            Assert.True(File.Exists(Path.Combine(dir, "schemas", "alpha.schema.json")));
            Assert.True(File.Exists(Path.Combine(dir, "examples", "beta.json")));

            // The whole point of the self-check: what `new` writes, `validate` accepts.
            var validated = await CliTestSupport.RunAsync(new ValidateCommand(CliTestSupport.Loader()), dir);
            Assert.Equal(0, validated.Exit);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }

    [Fact]
    public async Task Refuses_to_overwrite_without_force()
    {
        var dir = CliTestSupport.TempDir();
        try
        {
            var first = await CliTestSupport.RunAsync(new NewCommand(CliTestSupport.Writer()), "P", "-o", dir);
            Assert.Equal(0, first.Exit);

            var second = await CliTestSupport.RunAsync(new NewCommand(CliTestSupport.Writer()), "P", "-o", dir);
            Assert.Equal(1, second.Exit); // hard error: already exists

            var forced = await CliTestSupport.RunAsync(new NewCommand(CliTestSupport.Writer()), "P", "-o", dir, "--force");
            Assert.Equal(0, forced.Exit);
        }
        finally
        {
            CliTestSupport.Delete(dir);
        }
    }
}
