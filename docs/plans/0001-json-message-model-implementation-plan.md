# Implementation Plan — JSON-standardized message model

> **Implements:** [ADR-0001](../adr/integration/0001-json-standardized-message-model.md)
> **Trade-study:** [`docs/json-standardized-message-model.md`](../json-standardized-message-model.md)
> **Status:** ✅ implemented 2026-06-19 — backend 42 tests green, frontend 87 tests green,
> data-only sample verified end-to-end (auto-loads with no DLL; envelope publish → Redis HTTP 200).
> **Guiding principle:** ship as a *generalization* of the current pipeline. Nothing regresses;
> the existing `IPulsarPlugin` path keeps working until the new path is proven, then becomes a
> thin compatibility shim.

> ### Decisions locked (Phase 0) & deviations from the original plan
> - **0.1 adapter return type:** `byte[]`-returning delegate `JsonToRedisValue` in `Pulsar.Contracts`
>   (no Redis dependency); the transport already takes `byte[]`.
> - **0.2 message context:** yes — `JsonToRedisValue(string editedJson, MessageContext ctx)` with
>   `MessageContext(Key, Channel, Category)`.
> - **0.3 code-adapter reference:** `relative/or/abs/path.dll!Namespace.Type.Method` (public static),
>   resolved relative to the manifest, bound via `Delegate.CreateDelegate`.
> - **0.4 validator — DEVIATION:** shipped a **built-in, dependency-free advisory validator**
>   (`SchemaValidator`, a focused subset of draft 2020-12: type/required/properties/items/enum/const/
>   numeric bounds/lengths) **instead of `JsonSchema.Net`** — avoids a network package restore and
>   keeps `Pulsar.Core` dependency-light. Validation is advisory, so a full implementation isn't needed.
> - **0.5 example precedence:** manifest `example` file → schema `examples[0]` → schema object `default`
>   → skeleton from property defaults/types → `{}`.
> - **0.6 schema refs:** plain file path or `file.json#/json/pointer` (both implemented).
> - **Phase 3.1 envelope parity — DEVIATION:** the plan's "byte-for-byte" guard is impossible (the
>   envelope stamps a random `correlationId` and a wall-clock `emittedAtUnixMs`). Replaced with a
>   **structural** parity test: same envelope keys, `messageType == key`, and payload fields/values
>   round-trip. This is also the more correct test.

## Goal

A plugin can be **data only** — a `pulsar.plugin.json` manifest + per-message JSON Schemas +
examples + a named built-in adapter (`json-passthrough` / `json-envelope`) — with **no compiled
assembly**. A custom **`JsonToRedisValue` adapter** is supplied only for non-JSON wire formats.
Schema validation is **advisory, never blocking**. The transport, cyclic scheduler, SSE feed, and
HTTP surface stay intact.

## Definition of done

- [ ] The sample plugin runs as data only (manifest + schemas + `json-envelope`), zero plugin code.
- [ ] An existing `IPulsarPlugin` DLL still loads unchanged (back-compat shim).
- [ ] The publish path no longer requires a CLR `MessageType`; the `JSON → object → JSON` round-trip
      is gone for adapter-based plugins.
- [ ] Invalid-against-schema payloads still publish (advisory validation proven by a test).
- [ ] `dotnet test` green; the UI shows a schema-mismatch badge with "send anyway".
- [ ] README + ADR cross-references updated.

---

## Architecture target

```
 pulsar.plugin.json (manifest, data)            adapter (built-in name OR Assembly.dll!Type.Method)
   ├─ messages[]: key, displayName, category,        │
   │  defaultChannel, schema→*.schema.json,          ▼
   │  example→*.json                          JsonToRedisValue(string editedJson) -> RedisValue
   │                                                  │
 editor JSON ─► (advisory) validate(schema) ─► JsonToRedisValue ─► byte[] ─► IMessageTransport.PublishAsync
```

New `Pulsar.Contracts` surface (additive — existing interfaces stay):

```csharp
public delegate RedisValue JsonToRedisValue(string editedJson);          // the only required contract for code adapters

public sealed record MessageManifestEntry(
    string Key, string DisplayName, MessageCategory Category,
    string DefaultChannel, string SchemaPath, string? ExamplePath);

public sealed record PluginManifest(
    string Name, string Adapter, IReadOnlyList<MessageManifestEntry> Messages);
```

> Note: `RedisValue` lives in `StackExchange.Redis`, which `Pulsar.Contracts` must NOT reference
> (it is the dependency-free SDK). Resolve in Phase 1, Task 1.1 — likely the public delegate returns
> `byte[]` (or a `ReadOnlyMemory<byte>`), and `Pulsar.Redis` adapts `byte[] → RedisValue` at the edge.

---

## Phase 0 — Decisions to lock before coding

Resolve the open questions from the ADR so the contracts are stable:

| # | Question | Recommended default |
| --- | --- | --- |
| 0.1 | Adapter return type in `Pulsar.Contracts` (no Redis dep) | `byte[]` delegate; convert to `RedisValue` in `Pulsar.Redis` |
| 0.2 | Does the adapter need message context (key/channel)? | Yes — pass `JsonToRedisValue(string json, MessageContext ctx)` where `ctx` has `Key`, `Channel`, `Category` |
| 0.3 | Code-adapter reference syntax | `RelativeOrAbsolute.dll!Namespace.Type.Method` (public static), resolved relative to the manifest |
| 0.4 | Schema dialect + validator library | JSON Schema 2020-12; `JsonSchema.Net` (actively maintained, no codegen) |
| 0.5 | Example source precedence | manifest `example` file → schema `examples[0]` → schema `default`-built skeleton |
| 0.6 | One schema file per message vs shared | Support both: `schema` may be `file.json` or `file.json#/$defs/Name` |

**Acceptance:** a one-paragraph addendum appended to ADR-0001 (or a short `0.x` note here) recording
the chosen answers. Everything downstream depends on these.

---

## Phase 1 — Contracts (additive, non-breaking)

**1.1 Add the adapter + manifest types to `Pulsar.Contracts`.**
- `JsonToRedisValue` delegate (signature per 0.1/0.2), `MessageContext`, `MessageManifestEntry`,
  `PluginManifest`.
- Keep `IPulsarPlugin`, `IMessageCatalog`, `IMessageSerializer`, `MessageDescriptor` untouched.
- *Acceptance:* `Pulsar.Contracts` compiles with **no new package references**; xUnit reference test
  asserts the delegate is invokable and records still serialize round-trip with `System.Text.Json`.

**1.2 Introduce an internal `LoadedCatalog` abstraction in `Pulsar.Core`** that both the legacy
`IPulsarPlugin` and the new manifest path produce.
- Shape: `Name`, `IReadOnlyList<CatalogEntry>` where `CatalogEntry` has key/displayName/category/
  defaultChannel + a `Func<string /*templateJson*/>` (the example) + a `JsonToRedisValue` (the adapter)
  + an optional compiled schema handle.
- This is the seam that lets the rest of `Pulsar.Core` stop caring about CLR `MessageType`.
- *Acceptance:* compiles; not yet wired to anything.

---

## Phase 2 — Built-in adapters + manifest loader

**2.1 Built-in adapters in `Pulsar.Core`.**
- `json-passthrough`: returns `UTF8(editedJson)` verbatim.
- `json-envelope`: wraps payload in the envelope from `SampleJsonSerializer` (messageType =
  `ctx.Key`, correlationId, emittedAtUnixMs, payload).
- Registry: `IReadOnlyDictionary<string, JsonToRedisValue-factory>`; unknown name → clear error.
- *Acceptance:* unit tests for both adapters' output bytes (envelope shape asserted).

**2.2 Manifest loader (`ManifestPluginLoader`) producing a `LoadedCatalog`.**
- Parse `pulsar.plugin.json`; resolve schema + example paths relative to the manifest directory.
- Bind each entry's adapter: built-in name → registry; `Assembly.dll!Type.Method` → load that one
  assembly in an `AssemblyLoadContext` and bind the static method as `JsonToRedisValue`.
- Compile each schema once (cache the validator).
- Validate at load: each example must validate against its own schema (warn, don't fail) and every
  referenced file must exist (fail).
- *Acceptance:* loads the migrated sample (Phase 4) into a `LoadedCatalog`; bad paths/adapters
  produce actionable errors. Unit tests cover happy path + each failure mode.

**2.3 Advisory schema validation service.**
- `SchemaValidator.Validate(json, compiledSchema) → ValidationResult { bool Matches, string[] Messages }`.
- Pure function; never throws on invalid payloads.
- *Acceptance:* test proves an invalid payload returns `Matches=false` with messages and **does not
  throw**.

---

## Phase 3 — Wire the publish/template/cyclic paths to `LoadedCatalog`

**3.1 Refactor `MessagePublishService` to use the adapter, not the serializer + rehydrate.**
- Replace `Rehydrate(json) → Serialize(obj)` with `adapter(json, ctx)`.
- Remove the dependency on a CLR `MessageType` for adapter-based entries.
- Keep activity reporting identical (`PublishActivity`).
- *Acceptance:* `MessagePublishServiceTests` updated/green; published bytes for the envelope adapter
  byte-for-byte match the legacy `SampleJsonSerializer` output (regression guard).

**3.2 `MessageTemplateService` → template comes from the catalog entry's example, not reflection.**
- For adapter plugins: `CreateTemplateJson` returns the entry's example JSON (pretty-printed).
- Keep the reflection path only inside the legacy shim.
- *Acceptance:* `MessageTemplateServiceTests` updated; `/api/messages/{key}` returns example JSON.

**3.3 `CyclicJob` uses the adapter per tick.**
- `MessagePublishService.Serialize(...)` call site in `CyclicJob.PublishTickAsync` swaps to
  `adapter(payloadJson, ctx)`; cyclic jobs now carry the edited JSON string, not a payload object.
- Preserve "fresh serialization each tick" so envelope timestamps/correlationIds still vary.
- *Acceptance:* `CyclicJobManagerTests` green; a cyclic envelope job shows changing correlationIds.

**3.4 `PluginHost` / `IPluginHost` operate on `LoadedCatalog`.**
- `Current` exposes the catalog; `FindMessage(key)` returns a `CatalogEntry`.
- Load context lifetime: dispose only when a code adapter actually allocated one.
- *Acceptance:* `PluginLoaderTests` + host tests green.

---

## Phase 4 — Migrate the sample to data-only

**4.1 Author `backend/samples/Pulsar.SampleMessages/pulsar.plugin.json`** with the six existing
messages (Heartbeat, Temperature, Battery, OperatorAlert, ModeChanged, SubsystemFault), each with
its current default channel/category/example values, adapter = `json-envelope`.

**4.2 Write one `*.schema.json` per message** capturing the POCO shape, enriched where it adds value
(enum for `Severity`/`Status`, `minimum` for counts, `required`), plus an `examples/*.json` per
message mirroring today's POCO initializer defaults.

**4.3 Point startup config at the manifest.**
- `PulsarOptions.PluginPath` accepts a manifest path (`.json`) or a DLL; `StartupBootstrapper`
  detects which. Default `appsettings.json` → the sample manifest.
- *Acceptance:* `dotnet run --project src/Pulsar.Api` auto-loads the sample with **no plugin DLL**;
  UI shows all six messages; "Send once" + "Start cyclic" publish identical envelopes to before.

**4.4 Keep the old compiled sample** in the repo (or a `legacy/` folder) purely to exercise the
back-compat shim in tests.

---

## Phase 5 — Back-compat shim for `IPulsarPlugin`

**5.1 Wrap a loaded `IPulsarPlugin` as a `LoadedCatalog`.**
- For each `MessageDescriptor`, build a `CatalogEntry` whose adapter is
  `json => legacySerializer.Serialize(Rehydrate(json, descriptor.MessageType), descriptor)` and whose
  example comes from `CreateTemplate()` reflected to JSON (today's behavior).
- *Acceptance:* the retained compiled sample (4.4) loads and behaves exactly as before;
  `PluginLoaderTests` cover both manifest and legacy DLLs.

---

## Phase 6 — Frontend: advisory validation UX

**6.1 Surface schema mismatch in the composer.**
- `/api/messages/{key}` returns schema info (or a `validate` endpoint); the composer shows a
  non-blocking badge listing mismatch messages, with **"Send anyway"** always enabled.
- Reuse the existing signal-based store + SCSS patterns; no new runtime deps (keep the SSE/no-extra-
  library posture).
- *Acceptance:* Jest tests for the badge state (valid / invalid / no-schema); Playwright e2e: edit an
  invalid payload → badge shows → publish still succeeds.

---

## Phase 7 — Docs, cleanup, validation

- [ ] Update `README.md` "Writing your own message plugin": lead with the **data-only manifest**
      path; demote the `IPulsarPlugin` path to "advanced / typed & binary formats".
- [ ] Document the manifest schema, the built-in adapter names, and the `Assembly.dll!Type.Method`
      reference syntax.
- [ ] Note in README that data-only plugins avoid the Smart App Control DLL-load issue.
- [ ] Flip ADR-0001 references; add the Phase-0 decisions addendum.
- [ ] Full `dotnet test` + frontend `npm test` green; manual smoke per `docs/mocks` flows.

---

## Sequencing & risk

```
Phase 0 (decisions)
   └─► Phase 1 (contracts) ─► Phase 2 (adapters + loader) ─► Phase 3 (wire pipeline)
                                                                   ├─► Phase 4 (migrate sample) ─► Phase 6 (frontend)
                                                                   └─► Phase 5 (back-compat shim)
                                                                                         └─► Phase 7 (docs/cleanup)
```

- Phases 1–3 are pure backend and independently testable; the tool keeps working via the legacy
  loader throughout.
- Phase 4 is the first user-visible payoff (zero-code sample).
- **Highest-risk items:** 0.1/0.2 (contract shape — get it right once), 3.1 byte-for-byte envelope
  parity (regression guard), and the advisory-validation invariant (explicit test).
- **Smart App Control:** verify correctness via build + targeted runs; data-only phases are immune,
  but the back-compat shim test (Phase 5) loads a fresh DLL and may be blocked on this machine —
  treat a `0x800711C7` load failure there as environmental, not a code defect.

## Out of scope (future ADRs)

- Generating manifests/schemas automatically from AsyncAPI/OpenAPI.
- Multiple concurrently-loaded plugins.
- Schema-driven *form* editing (beyond raw JSON) in the composer.
