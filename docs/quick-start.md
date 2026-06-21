# Pulsar — Quick Start (from scratch)

This guide takes you from a **clean machine** to a running Pulsar instance that is
publishing messages to Redis, with the dashboard open in your browser. It then walks
you through verifying the pipeline end-to-end and adding your own message — no prior
knowledge of the codebase required.

> **What is Pulsar?** A Redis Pub/Sub message generator. You point it at a *plugin*
> (the simplest one is just data — a manifest plus JSON Schemas), pick a message, edit
> its JSON, and publish it **once** or on a **cyclic** interval. It exists to exercise a
> downstream *event hub → dashboard → plugins* pipeline without standing up the real
> upstream publisher. See the [README](../README.md) for the bigger picture.

**Time to first message:** ~10 minutes (most of it installing prerequisites and the
first `npm install`).

---

## 0. Install the prerequisites

You need four things. Install whatever you're missing, then confirm each version.

| Tool | Version | Why | Get it |
| --- | --- | --- | --- |
| **.NET SDK** | **8.0** | Builds & runs the backend API | <https://dotnet.microsoft.com/download/dotnet/8.0> |
| **Node.js** | **20 LTS or newer** | Builds & runs the Angular frontend | <https://nodejs.org> |
| **Docker Desktop** | any recent | Easiest way to get Redis (skip if you already run Redis) | <https://www.docker.com/products/docker-desktop> |
| **Git** | any | Clone the repo | <https://git-scm.com> |

Verify (any shell — PowerShell, Git Bash, or a terminal):

```bash
dotnet --version     # expect 8.0.x  (8.0.422 or newer 8.0 feature band)
node --version       # expect v20.x or higher
npm --version
docker --version
git --version
```

> **About the .NET version.** Pulsar pins the SDK to the .NET 8 feature band in
> `backend/global.json`. If you also have .NET 9/10/preview installed, that's fine —
> the pin keeps the build on 8.0 **as long as you run `dotnet` commands from inside the
> `backend/` folder** (where `global.json` lives). If `dotnet --version` reports a 9.x
> from elsewhere, don't worry; what matters is the version *inside* `backend/`.

> **No Docker?** You can run any Redis on `localhost:6379` instead — a native install,
> WSL, Memurai on Windows, or a remote Redis (set its connection string in step 4).

---

## 1. Get the code

```bash
git clone <your-pulsar-repo-url> Pulsar
cd Pulsar
```

The repo has three parts you'll touch:

```
Pulsar/
├─ backend/        .NET 8 solution (API + core + redis + the sample plugin)
│  └─ global.json  pins the SDK — run dotnet commands from here
├─ frontend/       Angular 17.2.2 dashboard
└─ docker-compose.yml   convenience Redis
```

---

## 2. Start Redis

From the repo root:

```bash
docker compose up -d
```

This starts `redis:7-alpine` as a container named **`pulsar-redis`**, listening on
`localhost:6379`. Confirm it's healthy:

```bash
docker ps                                   # pulsar-redis should be "Up"
docker exec -it pulsar-redis redis-cli ping # → PONG
```

> Already have Redis? Skip this step and point Pulsar at it in step 4.

---

## 3. Run the backend API

Open a terminal and run the API **from the `backend/` folder** (so the pinned SDK is
used):

```bash
cd backend
dotnet run --project src/Pulsar.Api
```

The first run restores NuGet packages and compiles, so give it a moment. When it's
ready you'll see Kestrel listening on:

```
http://127.0.0.1:5179
```

What just happened, automatically:

- The build **copied the bundled "Sample Messages" plugin** (a data-only manifest +
  JSON Schemas + examples) into `src/Pulsar.Api/plugins/manifest/`.
- On startup the API **loaded that manifest** (the `Pulsar:PluginPath` setting) and
  **connected to Redis** at `localhost:6379`.

You can already use Pulsar at <http://127.0.0.1:5179> — it serves a built copy of the
UI. For day-to-day development, though, run the live-reload frontend in step 4.

> **The API binds to loopback (`127.0.0.1`) on purpose.** Pulsar can load and execute
> user-supplied plugin assemblies, so it must not be exposed to the network. Don't
> change the Kestrel binding unless you've added authentication.

Quick health check from another terminal:

```bash
curl http://127.0.0.1:5179/api/plugin       # → JSON describing the loaded "Sample Messages" plugin
curl http://127.0.0.1:5179/api/connection   # → Redis connection status
```

---

## 4. Run the frontend (dev server)

Open a **second** terminal:

```bash
cd frontend
npm install        # first time only — installs Angular & friends
npm start          # ng serve with live reload
```

This serves the dashboard at:

```
http://localhost:4200
```

The dev server proxies every `/api/*` request to the backend on `:5179` (see
`frontend/proxy.conf.json`), so the two talk to each other automatically — no CORS or
config needed. Open **<http://localhost:4200>** in your browser.

You should see the three-pane dashboard with the **Sample Messages** plugin already
loaded: a catalog on the left, a composer in the centre, and jobs + live activity on
the right.

---

## 5. Send your first message

In the browser:

1. In the **Catalog** (left), click **Heartbeat** (under *Telemetry*).
2. The **Compose & Publish** panel (centre) fills with the channel
   `telemetry.heartbeat` and an editable JSON payload seeded from the example.
3. Click **Send once**. A success toast appears and the publish shows up in **Live
   Activity** (bottom right), streamed live over Server-Sent Events.
4. Now click **Start cyclic** (try the 1s preset). Pulsar publishes on that interval;
   watch the job appear in **Cyclic Jobs** with a climbing count and the feed tick.
   Click **Stop** to halt it.

That's the whole loop: **pick → edit → publish (once or cyclic) → observe**.

---

## 6. Verify the pipeline end-to-end

Pulsar says it published — but did the bytes really hit Redis? Subscribe with
`redis-cli` and watch them arrive.

In a spare terminal, subscribe to everything on the sample channels:

```bash
docker exec -it pulsar-redis redis-cli psubscribe "telemetry.*" "events.*" "faults.*"
```

Now go back to the dashboard and **Send once** (or start a cyclic job). Each publish
prints in the subscriber as a message on its channel — e.g. on `telemetry.heartbeat`
you'll see a JSON envelope like:

```json
{ "messageType": "HeartbeatTelemetry", "correlationId": "…", "emittedAtUnixMs": …, "payload": { "deviceId": "device-001", … } }
```

(The envelope comes from the sample plugin's `json-envelope` adapter. A
`json-passthrough` plugin would publish your edited JSON verbatim.) Press `Ctrl+C` to
stop subscribing.

You've now confirmed the full path: **UI → API → Redis Pub/Sub → subscriber**.

---

## 7. (Optional) Single-process build

For a demo or a simple deployment you don't need two terminals. Build the frontend
into the API's `wwwroot`, then run only the API:

```bash
cd frontend
npm run build                              # outputs into ../backend/src/Pulsar.Api/wwwroot
cd ../backend
dotnet run --project src/Pulsar.Api        # full UI now served at http://127.0.0.1:5179
```

Open <http://127.0.0.1:5179> — same app, one process, no `:4200` dev server.

---

## 8. Configuration

The backend reads two settings from
`backend/src/Pulsar.Api/appsettings.json` under the `Pulsar` section:

```json
"Pulsar": {
  "PluginPath": "plugins/manifest/pulsar.plugin.json",
  "RedisConnectionString": "localhost:6379"
}
```

- **`PluginPath`** — the catalog to auto-load on startup. It points at a
  `pulsar.plugin.json` manifest. The default path is the sample manifest the build
  copies into place.
- **`RedisConnectionString`** — a StackExchange.Redis connection string. Change this to
  point at a different Redis (e.g. `my-host:6379,password=…`).

You can also change both **at runtime** from the **Settings** dialog (the gear icon):
paste a new Redis connection string, or load a different plugin by path. Loading a new
plugin stops any running cyclic jobs and swaps the catalog atomically.

---

## 9. Add your own message (data-only, no code)

The fastest way to make Pulsar emit *your* messages is a **data-only manifest** — no
assembly to compile. Here's a minimal one.

1. Create a folder for your plugin, e.g. `my-plugin/`, with this layout:

   ```
   my-plugin/
   ├─ pulsar.plugin.json
   ├─ schemas/
   │  └─ my-telemetry.schema.json
   └─ examples/
      └─ my-telemetry.json
   ```

2. **`my-plugin/pulsar.plugin.json`** — the catalog:

   ```jsonc
   {
     "name": "My Messages",
     "adapter": "json-passthrough",        // publish the edited JSON verbatim
     "messages": [
       {
         "key": "MyTelemetry",
         "displayName": "My Telemetry",
         "category": "Telemetry",
         "defaultChannel": "telemetry.mine",
         "schema": "schemas/my-telemetry.schema.json",
         "example": "examples/my-telemetry.json"
       }
     ]
   }
   ```

3. **`my-plugin/schemas/my-telemetry.schema.json`** — the message shape (drives the
   editor and advisory validation):

   ```json
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

4. **`my-plugin/examples/my-telemetry.json`** — what the editor is seeded with:

   ```json
   { "deviceId": "device-001", "status": "Nominal" }
   ```

5. In the dashboard, open **Settings** and load the path to your
   `my-plugin/pulsar.plugin.json` (or set it as `Pulsar:PluginPath` and restart the
   API). Your **My Telemetry** message now appears in the catalog, ready to send.

> **Built-in adapters:** `json-passthrough` (edited JSON, verbatim) and `json-envelope`
> (wraps it in `{ messageType, correlationId, emittedAtUnixMs, payload }`). For binary
> wire formats (protobuf, Avro, custom framing) you can supply a tiny `JsonToRedisValue`
> static method — see **Writing your own message plugin** in the [README](../README.md#writing-your-own-message-plugin).

> **Validation is advisory.** A payload that doesn't match its schema is flagged in the
> composer but never blocked — sending malformed messages on purpose is a feature (it's
> a fault-injection tool). The handiest copy-paste starting point is the working sample
> at `backend/samples/Pulsar.SampleMessages/manifest`.

---

## 10. Run the tests

**Backend** (xUnit unit + in-memory HTTP integration tests):

```bash
cd backend
dotnet test
```

**Frontend** (Jest):

```bash
cd frontend
npm test                 # single run
npm run test:coverage    # with coverage
```

**End-to-end** (Playwright):

```bash
cd frontend
npm run e2e
```

---

## 11. Troubleshooting

| Symptom | Cause & fix |
| --- | --- |
| **Dashboard loads but "Redis offline" / publishes fail** | Redis isn't running or the connection string is wrong. Run `docker compose up -d`, confirm `docker exec -it pulsar-redis redis-cli ping` → `PONG`, and check `RedisConnectionString` in `appsettings.json` (or the Settings dialog). |
| **`http://localhost:4200` can't reach the API** | The backend isn't running, or you started the frontend without the proxy. Make sure step 3 is up on `:5179` and you launched the frontend with `npm start` (which uses `proxy.conf.json`). |
| **Build picks the wrong .NET SDK / restore errors mentioning .NET 9+** | You ran `dotnet` from outside `backend/`, bypassing `global.json`. `cd backend` first. Confirm with `dotnet --version` *inside* `backend/` — it should report 8.0.x. |
| **Port already in use (5179 or 4200)** | Another process holds the port. Stop it, or change the Kestrel URL in `appsettings.json` (and the proxy `target` in `frontend/proxy.conf.json` to match) / pass `ng serve --port`. |
| **Windows blocks a freshly-built custom adapter DLL** | This is **Smart App Control** refusing to run a newly built, unsigned assembly. Prefer the **data-only manifest** plugin (section 9), which loads no assembly. If you need a custom adapter, build/sign/trust it using your normal Windows policy path. |
| **`npm install` is slow or fails** | Ensure Node is 20+. Delete `frontend/node_modules` and retry. Corporate proxies may need npm registry config. |
| **No `docker` command** | Install Docker Desktop, or skip Docker entirely and run any Redis on `localhost:6379` (then point `RedisConnectionString` at it). |

---

## Where to go next

- **[README](../README.md)** — full feature tour, the HTTP API table, and the
  architecture overview.
- **Writing plugins** — [README → Writing your own message plugin](../README.md#writing-your-own-message-plugin)
  covers manifests and custom adapters.
- **Message model** — [`docs/json-standardized-message-model.md`](json-standardized-message-model.md).
- **UI design reference** — open [`docs/mocks/index.html`](mocks/index.html) in a browser.
```
