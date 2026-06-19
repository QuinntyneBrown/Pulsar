using Pulsar.Api.Activity;
using Pulsar.Api.Contracts;
using Pulsar.Core;
using Pulsar.Core.Cyclic;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Pulsar.Core.Publishing;
using Pulsar.Core.Transport;

namespace Pulsar.Api.Endpoints;

/// <summary>All HTTP endpoints for the tool, grouped under <c>/api</c>.</summary>
public static class PulsarEndpoints
{
    public static IEndpointRouteBuilder MapPulsarApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        MapPlugin(api);
        MapMessages(api);
        MapPublish(api);
        MapCyclic(api);
        MapConnection(api);
        MapActivityStream(api);

        // Unknown /api/* paths return a clean 404 instead of falling through to the
        // SPA's index.html (which the global MapFallbackToFile would otherwise serve).
        api.MapFallback(() => Results.NotFound());

        return app;
    }

    private static void MapActivityStream(IEndpointRouteBuilder api)
    {
        // Server-Sent Events: a long-lived response that streams activity frames
        // until the browser disconnects. No client library required.
        api.MapGet("/activity/stream", async (HttpContext ctx, ActivityBroadcaster broadcaster, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            using var _ = broadcaster.Subscribe(out var reader);
            await ctx.Response.WriteAsync(": connected\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);

            try
            {
                await foreach (var frame in reader.ReadAllAsync(ct))
                {
                    await ctx.Response.WriteAsync(frame, ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected — expected
            }
        });
    }

    private static void MapPlugin(IEndpointRouteBuilder api)
    {
        api.MapGet("/plugin", (IPluginHost host) =>
        {
            var current = host.Current;
            return Results.Ok(new PluginStateDto(current is not null,
                current is null ? null : PluginInfoDto.From(current)));
        });

        api.MapPost("/plugin/load", async (LoadPluginRequest req, PluginManagementService plugins) =>
        {
            var plugin = await plugins.LoadAsync(req.Path);
            return Results.Ok(PluginInfoDto.From(plugin));
        });

        api.MapPost("/plugin/unload", async (PluginManagementService plugins) =>
        {
            await plugins.UnloadAsync();
            return Results.NoContent();
        });
    }

    private static void MapMessages(IEndpointRouteBuilder api)
    {
        api.MapGet("/messages", (IPluginHost host) =>
        {
            var messages = host.Current?.Messages.Select(MessageDto.From) ?? Enumerable.Empty<MessageDto>();
            return Results.Ok(messages);
        });

        api.MapGet("/messages/{key}", (string key, IPluginHost host, MessageTemplateService templates) =>
        {
            var plugin = host.Current ?? throw new NoPluginLoadedException();
            var descriptor = plugin.FindMessage(key) ?? throw new MessageNotFoundException(key);
            return Results.Ok(MessageDetailDto.From(descriptor, templates.CreateTemplateJson(descriptor)));
        });
    }

    private static void MapPublish(IEndpointRouteBuilder api)
    {
        api.MapPost("/publish", async (PublishRequest req, MessagePublishService publisher, CancellationToken ct) =>
        {
            var result = await publisher.PublishAsync(req.Key, req.Channel, req.PayloadJson, ct);
            return Results.Ok(result);
        });
    }

    private static void MapCyclic(IEndpointRouteBuilder api)
    {
        api.MapGet("/cyclic", (CyclicJobManager jobs) => Results.Ok(jobs.List()));

        api.MapPost("/cyclic", (StartCyclicRequest req, CyclicJobManager jobs) =>
        {
            var info = jobs.Start(new StartCyclicJobRequest(req.Key, req.Channel, req.IntervalMs, req.PayloadJson));
            return Results.Created($"/api/cyclic/{info.Id}", info);
        });

        api.MapPost("/cyclic/{id:guid}/stop", (Guid id, CyclicJobManager jobs) =>
        {
            var info = jobs.Stop(id);
            return info is null ? Results.NotFound() : Results.Ok(info);
        });

        api.MapDelete("/cyclic/{id:guid}", (Guid id, CyclicJobManager jobs) =>
            jobs.Remove(id) ? Results.NoContent() : Results.NotFound());
    }

    private static void MapConnection(IEndpointRouteBuilder api)
    {
        api.MapGet("/connection", (IMessageTransport transport) => Results.Ok(transport.Status));

        api.MapPost("/connection", async (SetConnectionRequest req, IMessageTransport transport, CancellationToken ct) =>
        {
            var status = await transport.ConnectAsync(req.ConnectionString, ct);
            return Results.Ok(status);
        });
    }
}
