# Pulsar

**A Redis Pub/Sub message generator for testing event-driven pipelines.**

Pulsar lets you publish **cyclic** (recurring) messages and **one-shot** events or
faults onto Redis Pub/Sub channels — so you can exercise a downstream
*event hub → dashboard → plugins* pipeline without standing up the real upstream
that normally feeds it.

The key idea: **Pulsar does not know your message types or wire format.** You point
it at a **plugin** that declares which messages exist and how their edited JSON turns
into the bytes on the wire. The simplest plugin is **data only** — a
`pulsar.plugin.json` manifest plus a JSON Schema per message — and needs no compiled
code at all. Pulsar handles everything else: scheduling, editing, validation,
publishing, and a live activity feed.

```
                 ┌──────────────── Pulsar ─────────────────┐
 manifest +      │  catalog (data) + JsonToRedisValue       │   Redis Pub/Sub      your system
 JSON Schemas    ├──────────────────────────────────────────┤  ─────────────────►  under test
 (+ optional     │  scheduler · editor · validation · feed  │  channel: telemetry.* (event hub →
  adapter code)  └──────────────────────────────────────────┘                       dashboard → plugins)
```

A message library supplies the **JSON Schema** for each message; Pulsar creates JSON
from it, then a tiny **`JsonToRedisValue`** adapter converts that JSON into the Redis
message. Built-in adapters (`json-passthrough`, `json-envelope`) cover the common
cases with no code; the typed `IPulsarPlugin` model is still supported for binary
wire formats (see [Writing your own message plugin](#writing-your-own-message-plugin)).

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
`backend/src/Pulsar.Api/appsettings.json`. `PluginPath` may point at a **manifest**
(`*.json`, data-only) or a **plugin assembly** (`*.dll`, legacy):

```json
"Pulsar": {
  "PluginPath": "plugins/manifest/pulsar.plugin.json",
  "RedisConnectionString": "localhost:6379"
}
```

---

## Writing your own message plugin

There are two ways to write a plugin. Prefer the first.

### 1. Data-only manifest (recommended — no code)

A plugin can be just data: a `pulsar.plugin.json` manifest, one JSON Schema per
message, an example payload per message, and a named adapter. There is **no assembly
to build or load**, which also sidesteps Windows Smart App Control blocking
freshly-built unsigned plugin DLLs.

```jsonc
// pulsar.plugin.json — the catalog as data
{
  "name": "My Messages",
  "adapter": "json-envelope",          // built-in; or "json-passthrough", or a custom ref (below)
  "messages": [
    {
      "key": "MyTelemetry",
      "displayName": "My Telemetry",
      "category": "Telemetry",
      "defaultChannel": "telemetry.mine",
      "schema": "schemas/my-telemetry.schema.json",
      "example": "examples/my-telemetry.json"   // optional; falls back to the schema's examples/default
    }
  ]
}
```

```jsonc
// schemas/my-telemetry.schema.json — the message shape (drives the editor + advisory validation)
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "My Telemetry",
  "type": "object",
  "properties": {
    "deviceId": { "type": "string", "minLength": 1, "default": "device-001" },
    "status":   { "type": "string", "enum": ["Nominal", "Degraded", "Down"], "default": "Nominal" }
  },
  "required": ["deviceId", "status"]
}
```

**Built-in adapters** (the `adapter` field):

| Name | What it publishes |
| --- | --- |
| `json-passthrough` | The edited JSON, verbatim (UTF-8). |
| `json-envelope` | `{ messageType, correlationId, emittedAtUnixMs, payload }` wrapping the edited JSON. |

The schema is used for **advisory validation only** — a payload that doesn't match is
flagged in the composer, but you can always send it anyway (Pulsar is a fault-injection
tool, so sending malformed messages on purpose is a feature).

A complete, working example lives in
[`backend/samples/Pulsar.SampleMessages/manifest`](backend/samples/Pulsar.SampleMessages/manifest) —
copy it as your starting point.

### 2. Custom adapter (for non-JSON wire formats)

When the wire format is binary (protobuf, Avro, a bespoke framing), supply a
`public static` method matching `JsonToRedisValue` and reference it from the manifest
as `relative/path.dll!Namespace.Type.Method`:

```csharp
using Pulsar.Contracts;

public static class MyWire
{
    // byte[] (string editedJson, MessageContext context)
    public static byte[] ToRedis(string editedJson, MessageContext context)
        => MyBinaryFormat.Encode(editedJson, context.Key); // parse the JSON, emit any bytes you like
}
```

```jsonc
{ "name": "My Messages", "adapter": "MyWire.dll!MyNamespace.MyWire.ToRedis", "messages": [ /* … */ ] }
```

Reference `Pulsar.Contracts` without shipping a duplicate:

```xml
<ProjectReference Include="path/to/Pulsar.Contracts.csproj">
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</ProjectReference>
```

### 3. Legacy `IPulsarPlugin` (still supported)

The original typed model — a class library implementing `IPulsarPlugin` with a
`MessageDescriptor` catalog and an `IMessageSerializer` — still loads unchanged when
`PluginPath` points at the DLL. Internally it is adapted to the same pipeline (the
serializer becomes a `JsonToRedisValue`). The compiled reference plugin lives in
[`backend/samples/Pulsar.SampleMessages`](backend/samples/Pulsar.SampleMessages).

Load any of these via the **Settings** dialog (paste the path) or by setting
`Pulsar:PluginPath`. Loading a new plugin stops any running cyclic jobs and swaps the
catalog atomically.

---

## Architecture

Radically simple, SOLID, and message-agnostic. The repo splits into a self-contained
`backend/` (the .NET solution, `global.json`, `Directory.Build.props`) and `frontend/`
(the Angular workspace).

| Project | Responsibility |
| --- | --- |
| **backend/src/Pulsar.Contracts** | The plugin SDK: `JsonToRedisValue` + `MessageContext` (the adapter contract), plus the legacy `IPulsarPlugin`/`IMessageSerializer`/`MessageDescriptor`. The only thing a plugin references. |
| **backend/src/Pulsar.Core** | Domain logic — manifest + legacy plugin loading (`CatalogLoader`), built-in adapters, advisory JSON-Schema validation, the editor template, one-shot publishing, the cyclic scheduler, and the transport/activity abstractions. No web, no Redis client. |
| **backend/src/Pulsar.Redis** | The single place a Redis client appears: `IMessageTransport` over `StackExchange.Redis`. |
| **backend/src/Pulsar.Api** | ASP.NET Core 8 host — minimal-API endpoints, the SSE activity stream, and SPA hosting. |
| **backend/samples/Pulsar.SampleMessages** | The reference plugin in both forms: a data-only `manifest/` (schemas + examples + `json-envelope`) and the legacy compiled `IPulsarPlugin`. |
| **frontend** | Angular 17.2.2 standalone app: a signal-based store, an SSE live feed, and a three-pane dashboard with advisory schema validation. |
| **backend/tests/Pulsar.Tests** | xUnit unit + in-memory HTTP integration tests. |

Design choices worth knowing:

- **The catalog is data, not code.** A manifest + JSON Schemas describe the messages;
  the editor is seeded from an example and the message is published by a named adapter.
  No assembly is loaded for the data-only path — which also avoids Smart App Control
  blocking freshly-built plugin DLLs.
- **One seam, `JsonToRedisValue`.** The edited JSON goes straight to an adapter that
  returns the bytes — a strict generalization of the old serializer (the legacy
  `IMessageSerializer` is adapted to exactly this). Built-in adapters cover plain and
  enveloped JSON; a custom static method covers binary formats. Only a custom code
  adapter or a legacy DLL loads a collectible `AssemblyLoadContext`.
- **Validation is advisory.** A JSON-Schema mismatch is surfaced in the UI but never
  blocks publishing — sending malformed payloads on purpose is part of the job.
- **Dependency inversion** keeps `Pulsar.Core` free of both ASP.NET and Redis: it
  depends on `IMessageTransport` and `IActivityNotifier`, implemented by the host.
- **Server-Sent Events** (not a websocket library) power the live feed — the feed is
  server→client only, so SSE is the smallest thing that works.

### HTTP API

| Method & path | Purpose |
| --- | --- |
| `GET /api/plugin` · `POST /api/plugin/load` · `POST /api/plugin/unload` | Plugin state / load / unload |
| `GET /api/messages` · `GET /api/messages/{key}` | Catalog list / detail (with editable template) |
| `POST /api/messages/{key}/validate` | Advisory schema check (never blocks publishing) |
| `POST /api/publish` | Publish one message now |
| `GET /api/cyclic` · `POST /api/cyclic` · `POST /api/cyclic/{id}/stop` · `DELETE /api/cyclic/{id}` | Cyclic jobs |
| `GET /api/connection` · `POST /api/connection` | Redis connection status / (re)connect |
| `GET /api/activity/stream` | Server-Sent Events live feed |

---

## Tests

```bash
cd backend && dotnet test    # backend unit + integration tests
```
