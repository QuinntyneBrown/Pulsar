# Pulsar

**A Redis Pub/Sub message generator for testing event-driven pipelines.**

Pulsar lets you publish **cyclic** (recurring) messages and **one-shot** events or
faults onto Redis Pub/Sub channels — so you can exercise a downstream
*event hub → dashboard → plugins* pipeline without standing up the real upstream
that normally feeds it.

The key idea: **Pulsar does not know your message types or wire format.** You point
it at a compiled .NET library that declares which messages exist and how to
serialize them. Pulsar handles everything else — scheduling, editing, publishing,
and a live activity feed.

```
            ┌──────────── Pulsar ────────────┐
 your DLL   │  catalog + serializer (plugin) │      Redis Pub/Sub        your system
 (messages) ├────────────────────────────────┤   ───────────────────►   under test
            │  scheduler · editor · feed · UI │   channel: telemetry.*   (event hub →
            └────────────────────────────────┘                           dashboard → plugins)
```

---

## Quick start

**Prerequisites:** .NET 8 SDK, Node 20+ (Angular CLI 17.2.2), and Redis.

```bash
# 1. Redis (or use any Redis you already have on localhost:6379)
docker compose up -d

# 2. Backend API (auto-loads the bundled sample plugin, serves the UI).
#    Run from backend/ so the pinned .NET 8 SDK (global.json) is used.
cd backend
dotnet run --project src/Pulsar.Api
#   → http://localhost:5179

# 3. Frontend dev server (hot reload; proxies /api to the backend) — new terminal
cd frontend
npm install
npm start
#   → http://localhost:4200
```

For a **single-process** deployment, build the UI into the API and just run the API:

```bash
cd frontend && npm run build              # outputs into ../backend/src/Pulsar.Api/wwwroot
cd ../backend && dotnet run --project src/Pulsar.Api   # UI is now served at http://localhost:5179
```

Open the app, and you'll see the bundled **Sample Messages** plugin already loaded.
Pick a message, edit the JSON payload, then **Send once** or **Start cyclic**.

---

## Using the UI

- **Catalog** (left) — every message the loaded plugin exposes, grouped by category
  (Telemetry / Event / Fault / …). Click one to compose it.
- **Compose & Publish** (centre) — edit the Redis channel and the JSON payload, then:
  - **Send once** — publish a single message (great for events & faults).
  - **Start cyclic** — publish on a fixed interval (great for telemetry). Use the
    presets or type any interval ≥ 10 ms.
- **Cyclic Jobs** (right, top) — every running/stopped job with live counts; stop or remove.
- **Live Activity** (right, bottom) — every publish as it happens, streamed over SSE.
- **Settings** (gear) — change the Redis connection string or load a different plugin.

Configure the Redis connection and the auto-loaded plugin in
`backend/src/Pulsar.Api/appsettings.json`:

```json
"Pulsar": {
  "PluginPath": "plugins/Pulsar.SampleMessages.dll",
  "RedisConnectionString": "localhost:6379"
}
```

---

## Writing your own message plugin

A plugin is an ordinary .NET class library that references **only**
`Pulsar.Contracts` and implements `IPulsarPlugin`. Pulsar discovers it by
reflection when you load the DLL — there is nothing to register.

```csharp
using Pulsar.Contracts;

public sealed class MyPlugin : IPulsarPlugin
{
    public string Name => "My Messages";

    public IMessageCatalog Catalog { get; } = new MessageCatalog(new[]
    {
        new MessageDescriptor(
            key:            "MyTelemetry",
            displayName:    "My Telemetry",
            category:       MessageCategory.Telemetry,
            messageType:    typeof(MyTelemetry),
            defaultChannel: "telemetry.mine",
            createTemplate: () => new MyTelemetry { /* realistic defaults */ }),
    });

    // The serializer OWNS the wire format. Pulsar just calls it.
    public IMessageSerializer Serializer { get; } = new MySerializer();
}
```

The serializer turns a payload object into the exact bytes placed on Redis. This is
where you reproduce whatever envelope/encoding your system under test expects:

```csharp
public sealed class MySerializer : IMessageSerializer
{
    public byte[] Serialize(object message, MessageDescriptor descriptor)
    {
        // wrap, add a type discriminator, stamp a timestamp, encode — your call.
        return MyWireFormat.Encode(message, descriptor.Key);
    }
}
```

**Reference it from your `.csproj` without shipping a duplicate of the contracts:**

```xml
<ProjectReference Include="path/to/Pulsar.Contracts.csproj">
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</ProjectReference>
```

Build it, then load it via the **Settings** dialog (paste the path to the DLL) or by
setting `Pulsar:PluginPath`. Loading a new plugin stops any running cyclic jobs and
swaps the catalog and serializer atomically.

A complete, working example lives in [`backend/samples/Pulsar.SampleMessages`](backend/samples/Pulsar.SampleMessages) —
copy it as your starting point.

---

## Architecture

Radically simple, SOLID, and message-agnostic. The repo splits into a self-contained
`backend/` (the .NET solution, `global.json`, `Directory.Build.props`) and `frontend/`
(the Angular workspace).

| Project | Responsibility |
| --- | --- |
| **backend/src/Pulsar.Contracts** | The plugin SDK: `IPulsarPlugin`, `IMessageCatalog`, `IMessageSerializer`, `MessageDescriptor`. The only thing a plugin references. |
| **backend/src/Pulsar.Core** | Domain logic — plugin loading (isolated `AssemblyLoadContext`), JSON template ↔ instance bridging, one-shot publishing, the cyclic scheduler, and the transport/activity abstractions. No web, no Redis client. |
| **backend/src/Pulsar.Redis** | The single place a Redis client appears: `IMessageTransport` over `StackExchange.Redis`. |
| **backend/src/Pulsar.Api** | ASP.NET Core 8 host — minimal-API endpoints, the SSE activity stream, and SPA hosting. |
| **backend/samples/Pulsar.SampleMessages** | A reference plugin (telemetry/event/fault messages + a JSON-envelope serializer). |
| **frontend** | Angular 17.2.2 standalone app: a signal-based store, an SSE live feed, and a three-pane dashboard. |
| **backend/tests/Pulsar.Tests** | xUnit unit + in-memory HTTP integration tests. |

Design choices worth knowing:

- **The tool never compile-time references message types.** Plugins load into a
  collectible `AssemblyLoadContext`; `Pulsar.Contracts` resolves from the default
  context so interface identity holds across the boundary.
- **Pulsar only edits the *payload*.** It reflects a template to JSON, lets you edit
  it, rehydrates the concrete type, and hands it to the plugin's serializer — which
  alone decides the bytes on the wire.
- **Dependency inversion** keeps `Pulsar.Core` free of both ASP.NET and Redis: it
  depends on `IMessageTransport` and `IActivityNotifier`, implemented by the host.
- **Server-Sent Events** (not a websocket library) power the live feed — the feed is
  server→client only, so SSE is the smallest thing that works.

### HTTP API

| Method & path | Purpose |
| --- | --- |
| `GET /api/plugin` · `POST /api/plugin/load` · `POST /api/plugin/unload` | Plugin state / load / unload |
| `GET /api/messages` · `GET /api/messages/{key}` | Catalog list / detail (with editable template) |
| `POST /api/publish` | Publish one message now |
| `GET /api/cyclic` · `POST /api/cyclic` · `POST /api/cyclic/{id}/stop` · `DELETE /api/cyclic/{id}` | Cyclic jobs |
| `GET /api/connection` · `POST /api/connection` | Redis connection status / (re)connect |
| `GET /api/activity/stream` | Server-Sent Events live feed |

---

## Tests

```bash
cd backend && dotnet test    # backend unit + integration tests
```
