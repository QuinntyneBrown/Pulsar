using System.Net;
using Pulsar.Core;

namespace Pulsar.Api.Middleware;

/// <summary>
/// Translates the domain's expected exceptions into clean HTTP responses, so
/// endpoint handlers can stay free of try/catch noise.
/// </summary>
public sealed class PulsarExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PulsarExceptionMiddleware> _logger;

    public PulsarExceptionMiddleware(RequestDelegate next, ILogger<PulsarExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (TryMap(ex, out var status))
        {
            _logger.LogWarning(ex, "Request failed: {Message}", ex.Message);
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }

    private static bool TryMap(Exception ex, out HttpStatusCode status)
    {
        status = ex switch
        {
            NoPluginLoadedException => HttpStatusCode.Conflict,            // 409
            MessageNotFoundException => HttpStatusCode.NotFound,          // 404
            MessageEditException => HttpStatusCode.BadRequest,            // 400
            PluginLoadException => HttpStatusCode.BadRequest,             // 400
            SerializationFailedException => HttpStatusCode.UnprocessableEntity, // 422
            PublishFailedException => HttpStatusCode.BadGateway,          // 502
            PulsarException => HttpStatusCode.BadRequest,                 // 400 — any future user-facing subtype
            ArgumentException => HttpStatusCode.BadRequest,               // 400 (incl. ArgumentOutOfRange)
            _ => 0,
        };
        return status != 0;
    }
}
