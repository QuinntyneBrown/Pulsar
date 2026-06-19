namespace Pulsar.Core.Transport;

/// <summary>Current state of the connection to the message transport (Redis).</summary>
public sealed record TransportStatus(bool IsConnected, string? Endpoint, string? Error)
{
    public static TransportStatus Disconnected(string? error = null) => new(false, null, error);
    public static TransportStatus Connected(string endpoint) => new(true, endpoint, null);
}

/// <summary>
/// Abstracts the message bus Pulsar publishes onto. Core depends only on this;
/// the concrete Redis implementation lives in <c>Pulsar.Redis</c> so the domain
/// never references a specific client library (Dependency Inversion).
/// </summary>
public interface IMessageTransport
{
    /// <summary>The last known connection status.</summary>
    TransportStatus Status { get; }

    /// <summary>(Re)connects using the given connection string and returns the resulting status.</summary>
    Task<TransportStatus> ConnectAsync(string connectionString, CancellationToken ct = default);

    /// <summary>Publishes raw bytes to a Pub/Sub channel.</summary>
    Task PublishAsync(string channel, byte[] payload, CancellationToken ct = default);
}
