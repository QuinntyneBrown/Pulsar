using System.Text;
using System.Text.Json;
using Pulsar.Contracts;
using Pulsar.Core;
using Pulsar.Core.Plugins;
using Xunit;

namespace Pulsar.Tests;

public class CatalogLoaderTests
{
    private static CatalogLoader Loader() => TestSupport.CatalogLoader();

    private static byte[] Invoke(CatalogEntry entry, string json) =>
        entry.Adapter(json, new MessageContext(entry.Key, entry.DefaultChannel, entry.Category));

    // ---- manifest (data-only) path ----------------------------------------

    [Fact]
    public void Manifest_loads_catalog_with_schemas_and_examples()
    {
        var loaded = Loader().Load(TestSupport.SampleManifestPath);

        Assert.Equal("Sample Messages", loaded.Name);
        Assert.Equal(6, loaded.Messages.Count);

        var heartbeat = loaded.FindMessage("HeartbeatTelemetry")!;
        Assert.NotNull(heartbeat.Schema);                       // schema attached for advisory validation
        Assert.Contains("device-001", heartbeat.CreateTemplateJson()); // example seeds the editor
    }

    [Fact]
    public void Manifest_envelope_adapter_wraps_payload()
    {
        var loaded = Loader().Load(TestSupport.SampleManifestPath);
        var entry = loaded.FindMessage("HeartbeatTelemetry")!;

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Invoke(entry, entry.CreateTemplateJson())));
        var root = doc.RootElement;

        Assert.Equal("HeartbeatTelemetry", root.GetProperty("messageType").GetString());
        Assert.True(root.TryGetProperty("correlationId", out _));
        Assert.True(root.TryGetProperty("emittedAtUnixMs", out _));
        Assert.Equal("device-001", root.GetProperty("payload").GetProperty("deviceId").GetString());
    }

    [Fact]
    public void Manifest_with_unknown_adapter_throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pulsar-bad-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var schema = Path.Combine(dir, "s.json");
        File.WriteAllText(schema, "{\"type\":\"object\"}");
        var manifest = Path.Combine(dir, "pulsar.plugin.json");
        File.WriteAllText(manifest,
            "{\"name\":\"X\",\"adapter\":\"does-not-exist\",\"messages\":[{\"key\":\"k\",\"defaultChannel\":\"c\",\"schema\":\"s.json\"}]}");
        try
        {
            Assert.Throws<PluginLoadException>(() => Loader().Load(manifest));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_manifest_throws_PluginLoadException()
    {
        Assert.Throws<PluginLoadException>(() => Loader().Load("does-not-exist.json"));
    }

    // ---- legacy IPulsarPlugin path (back-compat shim) ----------------------

    [Fact]
    public void Legacy_assembly_loads_and_reads_catalog()
    {
        var loaded = Loader().Load(TestSupport.SamplePluginPath);

        Assert.Equal("Sample Messages", loaded.Name);
        Assert.Equal(6, loaded.Messages.Count);
        Assert.Contains(loaded.Messages, m => m.Key == "HeartbeatTelemetry");
        Assert.Contains(loaded.Messages, m => m.Key == "SubsystemFault");
        Assert.Null(loaded.FindMessage("HeartbeatTelemetry")!.Schema); // legacy has no schema
    }

    [Fact]
    public void Legacy_adapter_produces_the_same_envelope_shape_as_the_manifest()
    {
        var entry = Loader().Load(TestSupport.SamplePluginPath).FindMessage("HeartbeatTelemetry")!;

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Invoke(entry, entry.CreateTemplateJson())));
        var root = doc.RootElement;

        Assert.Equal("HeartbeatTelemetry", root.GetProperty("messageType").GetString());
        Assert.True(root.TryGetProperty("payload", out var payload));
        Assert.Equal("device-001", payload.GetProperty("deviceId").GetString());
    }

    [Fact]
    public void Missing_assembly_throws_PluginLoadException()
    {
        Assert.Throws<PluginLoadException>(() => Loader().Load("does-not-exist.dll"));
    }

    [Fact]
    public void Loaded_plugin_dll_is_not_locked_on_disk()
    {
        // Copy to a fresh path the test host has not itself loaded, so only our loader
        // touches it. The rebuild-and-reload loop requires the file to stay unlocked,
        // which the stream-based load guarantees.
        var temp = Path.Combine(Path.GetTempPath(), $"pulsar-plugin-{Guid.NewGuid():N}.dll");
        File.Copy(TestSupport.SamplePluginPath, temp, overwrite: true);
        try
        {
            var loaded = Loader().Load(temp);
            Assert.Equal("Sample Messages", loaded.Name);
            loaded.Dispose(); // unload the context so the file is released

            File.Delete(temp); // throws IOException if the DLL is locked
            Assert.False(File.Exists(temp));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
