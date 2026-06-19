namespace Pulsar.Contracts;

/// <summary>
/// Classifies a message so the UI can group it sensibly. Categories carry no
/// behaviour — they only drive presentation (e.g. cyclic telemetry vs. one-shot
/// events and faults).
/// </summary>
public enum MessageCategory
{
    /// <summary>Periodic readings — the natural fit for cyclic publishing.</summary>
    Telemetry,

    /// <summary>Discrete notifications, typically sent on demand.</summary>
    Event,

    /// <summary>Abnormal conditions, typically injected on demand to test handling.</summary>
    Fault,

    /// <summary>Requests/commands flowing toward the system under test.</summary>
    Command,

    /// <summary>Anything that does not fit the categories above.</summary>
    Other,
}
