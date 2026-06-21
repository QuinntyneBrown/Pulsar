using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Options;
using Pulsar.Cli.Abstractions;
using Pulsar.Cli.Rendering;
using Pulsar.Core;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Pulsar.Core.Publishing;
using Pulsar.Core.Transport;

namespace Pulsar.Cli.Commands;

/// <summary>
/// <c>pulsar publish &lt;manifest&gt; &lt;key&gt;</c> — load the plugin, connect to Redis,
/// and publish one message. The schema check is advisory: a payload that doesn't match is
/// warned about but still published (deliberately sending malformed payloads is the point
/// of a fault-injection tool).
/// </summary>
public sealed class PublishCommand : ICliCommand
{
    private readonly PluginManagementService _plugins;
    private readonly IMessageTransport _transport;
    private readonly MessagePublishService _publisher;
    private readonly IOptions<PulsarOptions> _options;

    public PublishCommand(
        PluginManagementService plugins,
        IMessageTransport transport,
        MessagePublishService publisher,
        IOptions<PulsarOptions> options)
    {
        _plugins = plugins;
        _transport = transport;
        _publisher = publisher;
        _options = options;
    }

    public Command Build()
    {
        var manifestArg = new Argument<string>("manifest", "A manifest (.json) or plugin folder.");
        var keyArg = new Argument<string>("key", "The message key to publish.");
        var channelOpt = new Option<string?>("--channel", "Override the message's default channel.");
        var payloadOpt = new Option<string?>("--payload", "Path to a JSON payload file (default: the message's example).");
        var stdinOpt = new Option<bool>("--stdin", "Read the JSON payload from standard input.");
        var redisOpt = new Option<string?>("--redis", "Redis connection string (default: the configured one).");

        var command = new Command("publish", "Publish one message to Redis (advisory schema check; never blocks).")
            { manifestArg, keyArg, channelOpt, payloadOpt, stdinOpt, redisOpt };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var manifest = PluginPaths.Resolve(ctx.ParseResult.GetValueForArgument(manifestArg));
            var key = ctx.ParseResult.GetValueForArgument(keyArg);
            var channel = ctx.ParseResult.GetValueForOption(channelOpt);
            var payloadFile = ctx.ParseResult.GetValueForOption(payloadOpt);
            var stdin = ctx.ParseResult.GetValueForOption(stdinOpt);
            var redis = ctx.ParseResult.GetValueForOption(redisOpt);
            ctx.ExitCode = await RunAsync(ctx.Console, manifest, key, channel, payloadFile, stdin, redis);
        });

        return command;
    }

    private async Task<int> RunAsync(
        System.CommandLine.IConsole console, string manifest, string key,
        string? channel, string? payloadFile, bool stdin, string? redis)
    {
        var catalog = await _plugins.LoadAsync(manifest); // PluginLoadException -> middleware -> exit 1
        var entry = catalog.FindMessage(key) ?? throw new MessageNotFoundException(key);

        string payload;
        if (stdin)
        {
            payload = await Console.In.ReadToEndAsync();
        }
        else if (!string.IsNullOrWhiteSpace(payloadFile))
        {
            if (!File.Exists(payloadFile))
                throw new PluginLoadException($"Payload file not found: {payloadFile}");
            payload = await File.ReadAllTextAsync(payloadFile);
        }
        else
        {
            payload = entry.CreateTemplateJson();
        }

        if (entry.Schema is not null)
        {
            var check = SchemaValidator.Validate(payload, entry.Schema);
            if (!check.Matches)
                foreach (var message in check.Messages)
                    ConsoleReport.Warn(console, $"payload does not match schema — {message}");
        }

        var connectionString = string.IsNullOrWhiteSpace(redis) ? _options.Value.RedisConnectionString : redis!;
        var status = await _transport.ConnectAsync(connectionString);
        if (!status.IsConnected)
            ConsoleReport.Warn(console, $"Redis not connected ({status.Error}); attempting to publish anyway.");

        var result = await _publisher.PublishAsync(key, channel, payload); // PublishFailedException -> middleware
        ConsoleReport.Line(console, $"Published {result.ByteCount} byte(s) to '{result.Channel}' at {result.Timestamp:O}.");
        return 0;
    }
}
