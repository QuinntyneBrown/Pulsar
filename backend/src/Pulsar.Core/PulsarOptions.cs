namespace Pulsar.Core;

/// <summary>Startup configuration, bound from the <c>Pulsar</c> section of appsettings.</summary>
public sealed class PulsarOptions
{
    public const string SectionName = "Pulsar";

    /// <summary>Optional path to a manifest to auto-load on startup.</summary>
    public string? PluginPath { get; set; }

    /// <summary>Redis connection string used to connect on startup.</summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";
}
