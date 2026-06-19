# Pulsar UI (Angular 17.2.2)

The frontend for **Pulsar**. See the [root README](../README.md) for full setup and
architecture.

- `npm start` — dev server on http://localhost:4200; proxies `/api` to the backend
  at http://localhost:5179 (see `proxy.conf.json`). Run the backend with
  `dotnet run --project ../backend/src/Pulsar.Api` alongside it.
- `npm run build` — production build, emitted into `../backend/src/Pulsar.Api/wwwroot`
  so the API can serve the UI as a single process.

Standalone components, a signal-based store (`src/app/core/pulsar.store.ts`), and a
native Server-Sent Events live feed (`src/app/core/activity.service.ts`). No unit or
e2e specs are wired up yet.
