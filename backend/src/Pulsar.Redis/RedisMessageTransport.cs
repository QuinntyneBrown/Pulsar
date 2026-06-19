using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Transport;
using StackExchange.Redis;

namespace Pulsar.Redis;

/// <summary>
/// Redis Pub/Sub implementation of <see cref="IMessageTransport"/>. Connecting is
/// non-fatal when Redis is unreachable (the tool stays up and reports the status,
/// so you can start Redis afterwards), and the connection can be re-pointed at a
/// different server at runtime.
/// </summary>
public sealed class RedisMessageTransport : IMessageTransport, IAsyncDisposable
{
    private readonly ILogger<RedisMessageTransport> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IConnectionMultiplexer? _mux;
    private volatile TransportStatus _status = TransportStatus.Disconnected("Not configured.");

    public RedisMessageTransport(ILogger<RedisMessageTransport>? logger = null)
        => _logger = logger ?? NullLogger<RedisMessageTransport>.Instance;

    public TransportStatus Status => _status;

    public async Task<TransportStatus> ConnectAsync(string connectionString, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _status = TransportStatus.Disconnected("A Redis connection string is required.");
            return _status;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var previous = _mux;
            ConfigurationOptions options;
            try
            {
                options = ConfigurationOptions.Parse(connectionString.Trim());
            }
            catch (Exception ex)
            {
                _status = TransportStatus.Disconnected($"Invalid connection string: {ex.Message}");
                return _status;
            }

            options.AbortOnConnectFail = false; // let the tool run even if Redis is down
            options.ConnectTimeout = Math.Min(options.ConnectTimeout, 3000); // snappy when Redis is down
            options.ConnectRetry = 1;

            try
            {
                var mux = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
                mux.ConnectionFailed += (_, e) =>
                {
                    _status = TransportStatus.Disconnected(e.Exception?.Message ?? "Connection failed.");
                    _logger.LogWarning("Redis connection failed: {Message}", e.Exception?.Message);
                };
                mux.ConnectionRestored += (_, _) =>
                {
                    _status = TransportStatus.Connected(Describe(mux));
                    _logger.LogInformation("Redis connection restored.");
                };

                _mux = mux;
                _status = mux.IsConnected
                    ? TransportStatus.Connected(Describe(mux))
                    : TransportStatus.Disconnected($"Could not reach Redis at '{Describe(mux)}'.");

                if (previous is not null)
                    await previous.DisposeAsync().ConfigureAwait(false);

                _logger.LogInformation("Redis transport configured for {Endpoint} (connected={Connected}).",
                    Describe(mux), mux.IsConnected);
            }
            catch (Exception ex)
            {
                _status = TransportStatus.Disconnected(ex.Message);
                _logger.LogWarning(ex, "Failed to connect to Redis.");
            }

            return _status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PublishAsync(string channel, byte[] payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("A channel is required.", nameof(channel));

        var mux = _mux
            ?? throw new InvalidOperationException("Redis is not configured. Set a connection string first.");

        ct.ThrowIfCancellationRequested();
        var subscriber = mux.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(channel), payload).ConfigureAwait(false);
    }

    private static string Describe(IConnectionMultiplexer mux)
    {
        var endpoint = mux.GetEndPoints().FirstOrDefault();
        return endpoint switch
        {
            DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
            IPEndPoint ip => $"{ip.Address}:{ip.Port}",
            not null => endpoint.ToString()!,
            _ => "redis",
        };
    }

    public async ValueTask DisposeAsync()
    {
        var mux = _mux;
        if (mux is not null)
            await mux.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
