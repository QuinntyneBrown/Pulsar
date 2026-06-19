using System.Text;
using Pulsar.Core;
using Pulsar.Core.Plugins;
using Xunit;

namespace Pulsar.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void Loads_sample_plugin_and_reads_catalog()
    {
        var loaded = new PluginLoader().Load(TestSupport.SamplePluginPath);

        Assert.Equal("Sample Messages", loaded.Name);
        Assert.Equal(6, loaded.Messages.Count);
        Assert.Contains(loaded.Messages, m => m.Key == "HeartbeatTelemetry");
        Assert.Contains(loaded.Messages, m => m.Key == "SubsystemFault");
    }

    [Fact]
    public void Serializer_wraps_payload_in_envelope()
    {
        var loaded = new PluginLoader().Load(TestSupport.SamplePluginPath);
        var descriptor = loaded.FindMessage("HeartbeatTelemetry")!;

        var bytes = loaded.Serializer.Serialize(descriptor.CreateTemplate(), descriptor);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"messageType\"", json);
        Assert.Contains("\"payload\"", json);
        Assert.Contains("deviceId", json); // payload property serialized via runtime type
    }

    [Fact]
    public void Missing_assembly_throws_PluginLoadException()
    {
        Assert.Throws<PluginLoadException>(() => new PluginLoader().Load("does-not-exist.dll"));
    }

    [Fact]
    public void Loaded_plugin_dll_is_not_locked_on_disk()
    {
        // Copy to a fresh path the test host has not itself loaded, so only our
        // loader touches it. The rebuild-and-reload loop requires the file to stay
        // unlocked, which the stream-based load guarantees.
        var temp = Path.Combine(Path.GetTempPath(), $"pulsar-plugin-{Guid.NewGuid():N}.dll");
        File.Copy(TestSupport.SamplePluginPath, temp, overwrite: true);
        try
        {
            var loaded = new PluginLoader().Load(temp);
            Assert.Equal("Sample Messages", loaded.Name);

            File.Delete(temp); // throws IOException if the DLL is locked
            Assert.False(File.Exists(temp));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
