namespace Pulsar.Core.Cyclic;

public enum CyclicJobState { Running, Stopped }

/// <summary>Immutable snapshot of a cyclic publishing job, safe to send to the UI.</summary>
public sealed record CyclicJobInfo(
    Guid Id,
    string MessageKey,
    string DisplayName,
    string Channel,
    int IntervalMs,
    CyclicJobState State,
    long PublishedCount,
    long FailureCount,
    string? LastError,
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt,
    DateTimeOffset? LastPublishedAt);
