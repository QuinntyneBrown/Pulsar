# ADR-0001: JSON-standardized message model with a `JsonToRedisValue` adapter

**Date:** 2026-06-19
**Category:** integration
**Status:** Accepted
**Deciders:** Quinntyne Brown

## Context

Pulsar publishes cyclic and one-shot messages onto Redis Pub/Sub to exercise a downstream
*event hub → dashboard → plugins* pipeline. Its defining property is that **the tool does not know
your message types or wire format** — a loaded plugin supplies both.

Today a plugin is a compiled .NET assembly that references `Pulsar.Contracts` and implements
`IPulsarPlugin` (`backend/src/Pulsar.Contracts/IPulsarPlugin.cs`), exposing:

- a **catalog** of `MessageDescriptor`s, each carrying a CLR `Type MessageType` and a
  `Func<object> CreateTemplate`, and
- an **`IMessageSerializer`** that turns a payload *object* into the bytes published on the wire.

The editing boundary is **already 100% JSON**: the UI reflects a template POCO to JSON, the user
edits JSON, and `MessageTemplateService.Rehydrate` deserializes that JSON back into the CLR
`MessageType` before handing the object to the serializer
(`MessagePublishService` → `IMessageTransport.PublishAsync(channel, byte[])`).

Three forces motivate revisiting this:

1. **A redundant round-trip.** For the common case where the wire format is JSON, the pipeline does
   `JSON → object → JSON`. With `System.Text.Json` Web defaults this round-trip is lenient and
   *lossy*: unknown fields are silently dropped, missing fields defaulted, types coerced. For a
   tool whose purpose includes injecting malformed/edge-case payloads, normalizing the payload away
   is actively wrong.
2. **High authoring + loading cost.** Every plugin — even one that just emits enveloped JSON —
   must be a compiled assembly implementing three interfaces, loaded into a collectible
   `AssemblyLoadContext` with careful `Pulsar.Contracts` type-identity handling. On this
   development machine, Smart App Control in enforce mode intermittently **blocks freshly-built
   unsigned plugin DLLs from loading**, breaking the rebuild-and-reload loop that is the point of
   the tool.
3. **The catalog is code, not data.** It cannot be diffed, hand-authored, or generated from the
   message contracts the downstream team already publishes (AsyncAPI/OpenAPI/JSON Schema).

A full trade-study is recorded in
[`docs/json-standardized-message-model.md`](../../json-standardized-message-model.md).

The key realization from that study: today's publish tail
`edited JSON → rehydrate to type → IMessageSerializer.Serialize(obj) → byte[]` is *itself* just one
function of the shape `string json → byte[]`. That function is exactly a `JsonToRedisValue` adapter.
So a JSON-schema + adapter model is a **strict generalization** of what exists, not a competing
design — the current serializer is one possible adapter.

## Decision

Adopt a **JSON-standardized message model** as Pulsar's primary plugin shape:

1. A **compatible message library supplies a JSON Schema per message** plus a **manifest**
   (`pulsar.plugin.json`) that lists messages with their key, display name, category, default
   channel, schema reference, and example reference. The catalog becomes **data, not code**.
2. The wire-format seam becomes a single **`JsonToRedisValue` adapter**: a function
   `string editedJson → RedisValue` (equivalently `byte[]`). **Plugins no longer implement an
   interface.**
3. Pulsar ships **built-in adapters** (`json-passthrough`, `json-envelope`) so a plugin can be
   **data only, with zero compiled code**. A custom adapter is supplied only when the wire format
   is non-trivial (binary, protobuf, bespoke framing), named in the manifest as an
   assembly-qualified method reference.
4. **Schema validation is advisory, never blocking** — the composer surfaces a "doesn't match
   schema" badge with an explicit "send anyway"; publish is never rejected.
5. The existing typed `IMessageSerializer` path is **retained as an advanced adapter** (it is the
   `string json → deserialize to T → Serialize(T) → bytes` special case), so current plugins keep
   working behind a back-compat loader.

The transport contract (`IMessageTransport.PublishAsync(channel, byte[])`) is **unchanged**.

## Options Considered

### Option 1: Keep the current `IPulsarPlugin` + `IMessageSerializer` model
- **Pros:** No work; typed object at the wire seam is convenient for binary formats; template can
  never drift from its type; one assembly is the single source of truth.
- **Cons:** Redundant, lossy `JSON → object → JSON` round-trip for the common JSON-on-wire case;
  every plugin must be a compiled assembly (and thus exposed to the Smart App Control DLL-load
  failures); catalog is code that can't be diffed or generated; three-interface surface to learn;
  validation is implicit and silently discards exactly the malformed input a test may intend.

### Option 2: JSON Schema + `JsonToRedisValue` adapter (chosen)
- **Pros:** Strict generalization — loses no capability (typed serialization remains available as an
  adapter); removes the redundant round-trip; enables **zero-code, data-only plugins** which also
  sidestep the SAC DLL-load problem entirely; catalog becomes diffable/versionable data
  generatable from AsyncAPI/OpenAPI; one-function contract; richer *explicit* validation;
  "send exactly what I typed" suits fault injection.
- **Cons:** Wire seam receives a string, so non-JSON formats must re-parse inside the adapter
  (optional cost, only those users pay it); template/example is now separate data that can drift
  from its schema; adds a JSON Schema validator dependency; discovery of code adapters must be made
  explicit in the manifest.

### Option 3: JSON-only on the wire (drop arbitrary `byte[]`, always publish JSON)
- **Pros:** Simplest possible model; no adapter needed at all; nothing to learn.
- **Cons:** **Removes Pulsar's core value** — the ability to reproduce *any* wire format a system
  under test expects (binary/protobuf/Avro/compressed/encrypted). Unacceptable regression; rejected.

## Consequences

### Positive
- Eliminates the redundant `JSON → object → JSON` round-trip for the dominant case and stops
  silently mutating payloads.
- **Zero-code plugins** (manifest + schemas + a built-in adapter) drastically cut authoring and
  onboarding friction.
- Data-only plugins have **no DLL to load**, so they are immune to the Smart App Control
  DLL-loading failures that intermittently break the current model on this machine.
- Catalog-as-data is reviewable, diffable, and **generatable** from contracts the downstream team
  already owns.
- Explicit JSON Schema validation (required/enum/range/pattern/format) is more informative than the
  current implicit coercion and can catch field typos the round-trip currently swallows.
- Contract surface shrinks from three interfaces to one function; message libraries need not depend
  on Pulsar at all in the data-only tier.

### Negative
- Non-JSON wire formats must parse the JSON string inside their adapter rather than receiving a
  typed object (usually a one-liner via the format's JSON mapping, but real extra work).
- Template/example data lives separately from the schema and can drift; requires a load-time
  validation guard to mitigate.
- A new third-party dependency (a JSON Schema validator) enters `Pulsar.Core`.

### Risks
- **Enforcing validation by mistake** would break the tool's fault-injection purpose. Mitigation:
  validation is advisory by contract; add a test asserting that an invalid payload still publishes.
- **Adapter discovery ambiguity** without an interface. Mitigation: the manifest names the adapter
  explicitly (built-in name or `Assembly.dll!Namespace.Type.Method`); fail loudly on a bad reference.
- **Migration breakage** for existing `IPulsarPlugin` plugins. Mitigation: keep the legacy loader,
  wrap it as an adapter, and migrate the sample first to prove the path.

## Implementation Notes

- Introduce `delegate RedisValue JsonToRedisValue(string editedJson)` and a `PluginManifest` /
  `MessageManifestEntry` record set in `Pulsar.Contracts`.
- Provide built-in `json-passthrough` and `json-envelope` adapters in `Pulsar.Core`.
- Load an `AssemblyLoadContext` **only** when a custom code adapter is referenced; data-only
  plugins never touch it.
- Re-express `backend/samples/Pulsar.SampleMessages` as a manifest + per-message schema + example
  files using the built-in `json-envelope` adapter, to validate the end-to-end path.
- Keep `IMessageTransport` and the SSE/cyclic machinery unchanged.
- A detailed, sequenced plan lives in
  [`docs/plans/0001-json-message-model-implementation-plan.md`](../../plans/0001-json-message-model-implementation-plan.md).

## References

- Trade-study: [`docs/json-standardized-message-model.md`](../../json-standardized-message-model.md)
- Implementation plan: [`docs/plans/0001-json-message-model-implementation-plan.md`](../../plans/0001-json-message-model-implementation-plan.md)
- Current contract: `backend/src/Pulsar.Contracts/IPulsarPlugin.cs`, `IMessageSerializer.cs`,
  `MessageDescriptor.cs`
- Current pipeline: `backend/src/Pulsar.Core/Messages/MessageTemplateService.cs`,
  `Publishing/MessagePublishService.cs`, `Plugins/PluginLoader.cs`
- Smart App Control constraint (machine-specific): see project memory `windows-smart-app-control`
