using System.Text.Json.Serialization;
using Pulsar.Api.Activity;
using Pulsar.Api.Endpoints;
using Pulsar.Api.Hosting;
using Pulsar.Api.Middleware;
using Pulsar.Core;
using Pulsar.Core.Activity;
using Pulsar.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PulsarOptions>(builder.Configuration.GetSection(PulsarOptions.SectionName));

// HTTP JSON: web defaults (camelCase) plus enums as readable strings.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The activity broadcaster powers the live SSE feed. Register it BEFORE
// AddPulsarCore so Core's no-op notifier fallback (TryAdd) is skipped.
builder.Services.AddSingleton<ActivityBroadcaster>();
builder.Services.AddSingleton<IActivityNotifier>(sp => sp.GetRequiredService<ActivityBroadcaster>());

builder.Services.AddPulsarCore();
builder.Services.AddPulsarRedis();
builder.Services.AddHostedService<StartupBootstrapper>();

const string DevCorsPolicy = "pulsar-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCorsPolicy, p => p
    .WithOrigins("http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseMiddleware<PulsarExceptionMiddleware>();

if (app.Environment.IsDevelopment())
    app.UseCors(DevCorsPolicy);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPulsarApi();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed for integration testing via WebApplicationFactory<Program>.
public partial class Program { }
