using Microsoft.Extensions.Options;
using Pulsar.Core;
using Pulsar.Core.Plugins;
using Pulsar.Core.Transport;

namespace Pulsar.Api.Hosting;

/// <summary>
/// On startup: connect to Redis and, if configured, auto-load a plugin. Both are
/// best-effort — failures are logged but never stop the app, so the UI can be
/// used to fix the configuration interactively.
/// </summary>
public sealed class StartupBootstrapper : IHostedService
{
    private readonly PulsarOptions _options;
    private readonly IMessageTransport _transport;
    private readonly PluginManagementService _plugins;
    private readonly IHostEnvironment _env;
    private readonly ILogger<StartupBootstrapper> _logger;

    public StartupBootstrapper(
        IOptions<PulsarOptions> options,
        IMessageTransport transport,
        PluginManagementService plugins,
        IHostEnvironment env,
        ILogger<StartupBootstrapper> logger)
    {
        _options = options.Value;
        _transport = transport;
        _plugins = plugins;
        _env = env;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var status = await _transport.ConnectAsync(_options.RedisConnectionString, cancellationToken);
        if (status.IsConnected)
            _logger.LogInformation("Connected to Redis at {Endpoint}.", status.Endpoint);
        else
            _logger.LogWarning("Redis not connected at startup: {Error}", status.Error);

        if (!string.IsNullOrWhiteSpace(_options.PluginPath))
        {
            var path = ResolvePath(_options.PluginPath);
            try
            {
                var plugin = await _plugins.LoadAsync(path);
                _logger.LogInformation("Auto-loaded plugin '{Name}' from {Path}.", plugin.Name, path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not auto-load plugin from {Path}.", path);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ResolvePath(string configured) =>
        Path.IsPathRooted(configured) ? configured : Path.Combine(_env.ContentRootPath, configured);
}
