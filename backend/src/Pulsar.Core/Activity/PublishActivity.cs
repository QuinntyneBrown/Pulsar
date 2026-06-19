namespace Pulsar.Core.Activity;

/// <summary>A single publish attempt — the unit shown in the live activity feed.</summary>
/// <param name="Source">Where the publish originated: <c>"manual"</c> or <c>"cyclic"</c>.</param>
/// <param name="JobId">The cyclic job that produced it, when <paramref name="Source"/> is cyclic.</param>
public sealed record PublishActivity(
    string Source,
    string MessageKey,
    string DisplayName,
    string Channel,
    int ByteCount,
    bool Success,
    string? Error,
    DateTimeOffset Timestamp,
    Guid? JobId);
