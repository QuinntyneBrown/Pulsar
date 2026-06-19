using Pulsar.Core.Cyclic;

namespace Pulsar.Core.Activity;

/// <summary>
/// Sink for live activity. Core raises events through this abstraction; the host
/// (API) implements it to push to the UI over Server-Sent Events (SSE). Keeping it
/// an interface means the domain has no dependency on the transport to the browser.
/// </summary>
public interface IActivityNotifier
{
    /// <summary>Raised after every publish attempt (success or failure).</summary>
    Task PublishedAsync(PublishActivity activity);

    /// <summary>Raised whenever a cyclic job is started, stopped, or removed.</summary>
    Task JobChangedAsync(CyclicJobInfo job);
}

/// <summary>No-op notifier used when no live feed is wired up (e.g. in tests).</summary>
public sealed class NullActivityNotifier : IActivityNotifier
{
    public Task PublishedAsync(PublishActivity activity) => Task.CompletedTask;
    public Task JobChangedAsync(CyclicJobInfo job) => Task.CompletedTask;
}
