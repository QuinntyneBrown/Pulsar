# Pulsar frontend — E2E tests

Transport-boundary ↔ UI tests for the Pulsar dashboard, written with
[Playwright](https://playwright.dev) and the **Page Object Model** pattern.

The **backend is fully mocked at the transport boundary**, so the entire UI is
verified without a running .NET API, Redis, or any network — `npm run e2e` is
completely self-contained.

## Running

```bash
npm run e2e          # headless run (boots its own ng serve)
npm run e2e:ui       # interactive Playwright UI mode
npm run e2e:report   # open the last HTML report
```

Playwright starts the Angular dev server itself (see `webServer` in
`playwright.config.ts`). If a dev server is already listening on
`http://localhost:4200`, it is reused.

## Layout

```
e2e/
├─ pages/        Page Object Models — one per panel + a composite root
│  ├─ dashboard.page.ts      root POM: navigation, toast, aggregates the others
│  ├─ header-bar.page.ts     plugin / Redis / live / cyclic status + Settings
│  ├─ catalog.page.ts        grouped, selectable message list
│  ├─ composer.page.ts       compose / format / publish / start-cyclic
│  ├─ jobs.page.ts           cyclic-job cards (JobCard scoped object)
│  ├─ activity-feed.page.ts  live activity rows (ActivityRow scoped object)
│  └─ settings.page.ts       connection + plugin load/unload modal
├─ fixtures/     Test infrastructure
│  ├─ backend-mock.ts        the transport mock (HTTP routes + SSE replacement)
│  ├─ data.ts                canonical fixture data + builders (typed from app models)
│  └─ test.ts                Playwright fixtures: `mock` + `app`
└─ tests/        Specs, one file per behaviour area
   ├─ initial-load.spec.ts   loaded / no-plugin / no-messages boot states
   ├─ catalog.spec.ts        selection, grouping, edit preservation
   ├─ composer.spec.ts       format / reset / inline JSON validation
   ├─ publish.spec.ts        publish-once success / custom channel / error / invalid
   ├─ cyclic-jobs.spec.ts    start / presets / validation / stop / remove / counters
   ├─ activity-feed.spec.ts  SSE rows, ordering, failure styling, clear
   ├─ connection.spec.ts     connect success / failure / disabled button
   ├─ plugin.spec.ts         load / unload / load failure
   ├─ live-status.spec.ts    SSE connect / drop / reconnect indicator
   └─ settings-dialog.spec.ts open / close / backdrop / click-inside
```

## How the mock works

`MockBackend` (`fixtures/backend-mock.ts`) stands in for the whole backend at the
two transport seams the app speaks:

1. **HTTP** — every `/api/**` request is intercepted with `page.route` and
   answered from in-memory state that mirrors the real controller semantics
   (plugin load/unload, messages, publish, cyclic CRUD, connection). State is
   read live per request, so a test configures it *before* `app.goto()`:

   ```ts
   mock.withNoPlugin();                       // boot state
   mock.failOn('POST publish', 400, {error}); // inject a failure
   mock.setConnectResult({ kind: 'fail', error: '...' });
   ```

2. **Server-Sent Events** — `window.EventSource` is replaced (via
   `addInitScript`) with a controllable mock, so the test drives the live feed
   deterministically:

   ```ts
   await mock.emitPublished(makeActivity({ ... }));
   await mock.emitJobChanged(makeJob({ id: 'job-1', publishedCount: 12 }));
   await mock.dropLive();   // simulate stream error
   await mock.goLive();     // simulate reconnect
   ```

The `app` fixture depends on `mock`, so the backend is mocked for **every** test
— including ones that never reference `mock` directly. (Playwright only sets up
fixtures a test mentions; without this dependency, such a test would hit the
real, dead proxy and silently see an empty app.)

## Selectors

Components expose stable `data-testid` attributes (and `data-key` / `data-job-id`
for keyed lookups). Page objects own all selectors; specs never touch raw
locators, so a markup change is absorbed in one place.
