# Exploring a JSON‑standardized message model for Pulsar

> **Status:** exploration / trade‑study (no decision taken yet)
> **Question:** Should Pulsar standardize on JSON at the message boundary — where a
> compatible message library supplies a **JSON Schema** for each message and a single
> **`JsonToRedisValue`** adapter turns edited JSON into the bytes published on Redis —
> instead of today's `IPulsarPlugin` (catalog of CLR types + `IMessageSerializer`)?

## TL;DR — the verdict

**Yes, it is better for Pulsar's actual job — with two caveats to get right.**

The decisive observation is that the proposed adapter is not a *different* model; it is a
**strict generalization** of the one we already have. Today's publish path ends with:

```
edited JSON ──► rehydrate to CLR MessageType ──► IMessageSerializer.Serialize(obj, descriptor) ──► byte[]
```

The middle two steps (`rehydrate → Serialize`) are *themselves* just one possible function
of the form `string json → byte[]`. That function — Pulsar's whole reason for needing a CLR
`MessageType` — is exactly the `JsonToRedisValue` adapter the proposal asks for. So:

- The proposed model **loses no capability**: any wire format the current serializer can
  produce, an adapter can still produce (it can deserialize to a type internally if it wants).
- It makes the **common case (JSON on the wire) dramatically simpler** and removes a
  redundant `JSON → object → JSON` round‑trip.
- It lets a plugin be **data (a schema + a manifest) with zero compiled code** in the common
  case — which, on this machine, also sidesteps the Smart App Control DLL‑loading problem that
  plagues freshly‑built plugin assemblies.

The two caveats: **(1)** validation must be *advisory*, not enforcing (a message‑injection tool
must be able to send deliberately malformed payloads); **(2)** the editor template now comes from
schema `examples`/manifest data rather than a CLR constructor, so we add a small amount of
authoring (or a generator) to keep templates realistic.

Recommendation: adopt the adapter model as the **primary, simplest** plugin shape, keep the
typed `IMessageSerializer` as an *advanced adapter* for binary formats, and ship a built‑in
`json‑passthrough` / `json‑envelope` adapter so the zero‑code path works out of the box. Details
and a migration path below.

---

## 1. Context: where JSON already lives today

Pulsar publishes cyclic and one‑shot messages onto Redis Pub/Sub to exercise a downstream
*event hub → dashboard → plugins* pipeline. Its defining design choice is that **the tool does
not know your message types or wire format** — a loaded plugin supplies both.

What is easy to miss is that **the editing boundary is already 100% JSON**. The UI never sees a
CLR type; it only ever shows and edits JSON. The CLR type exists purely on either side of that
JSON, to (a) seed a realistic template and (b) give the serializer a typed object to work from.

```
                      ┌──────────────── today: JSON is already the editing boundary ─────────────────┐
 CreateTemplate()  ─► CLR instance ─► reflect‑serialize ─► [ JSON in the editor ] ─► rehydrate ─► CLR ─► Serialize ─► byte[] ─► Redis
   (plugin code)                                              ▲ user edits here ▲                 (plugin code, IMessageSerializer)
```

So the proposal is not "introduce JSON." JSON is already the contract with the user. The proposal
is: **stop bouncing that JSON through a CLR type on the way to the wire, and let the message
shapes be described *as* JSON (a schema) rather than *as* CLR types.**

Grounding references in the codebase:

| Concern | Today's location |
| --- | --- |
| Plugin contract | `backend/src/Pulsar.Contracts/IPulsarPlugin.cs` (`Name` + `Catalog` + `Serializer`) |
| Message shape | `MessageDescriptor` — carries a CLR `Type MessageType` and a `Func<object> CreateTemplate` |
| Wire format seam | `IMessageSerializer.Serialize(object message, MessageDescriptor) : byte[]` |
| JSON ↔ object bridge | `Pulsar.Core/Messages/MessageTemplateService.cs` (`CreateTemplateJson`, `Rehydrate`) |
| Publish tail | `Pulsar.Core/Publishing/MessagePublishService.cs` → `IMessageTransport.PublishAsync(channel, byte[])` |
| Plugin loading | `Pulsar.Core/Plugins/PluginLoader.cs` + collectible `PluginLoadContext` (`AssemblyLoadContext`) |

---

## 2. The current model, precisely

A plugin is a compiled .NET class library that references `Pulsar.Contracts` and implements
`IPulsarPlugin`. It provides:

- **`Catalog`** — a list of `MessageDescriptor`, each pairing a stable `Key`, a `DisplayName`, a
  `MessageCategory`, a **`Type MessageType`**, a `DefaultChannel`, and a **`Func<object> CreateTemplate`**.
- **`Serializer`** — an `IMessageSerializer` that turns a payload *object* into the exact bytes on
  the wire (envelope, type discriminator, timestamp, compression, encryption — its choice).

Loading is non‑trivial by necessity (`PluginLoader`):

- The DLL is loaded **from a stream** into a **collectible `AssemblyLoadContext`** so the file is
  not locked and the assembly can be unloaded/reloaded (the rebuild‑and‑reload loop is the point
  of the tool).
- `Pulsar.Contracts` is deliberately **deferred to the default context** so interface identity
  holds across the boundary.
- Exactly one `IPulsarPlugin` implementation is located by reflection and instantiated via a
  public parameterless constructor.

**What the CLR `MessageType` actually buys us today:**

1. A realistic **template** — `CreateTemplate()` returns a populated POCO; reflection serializes it
   to the JSON the editor opens with.
2. A **typed object** for the serializer to consume (needed if the wire format is binary/typed).
3. An implicit **round‑trip** through `System.Text.Json` on `Rehydrate` (`JsonSerializerDefaults.Web`).

That third point is weaker than it looks. With Web defaults, deserialization is *lenient*: unknown
fields are **silently dropped**, missing fields are **defaulted**, and types are **coerced**. So the
round‑trip is not real validation — it mostly **normalizes** the payload and can quietly discard
exactly the malformed content a test might want to send.

---

## 3. The proposed model

A *compatible message library* supplies:

1. **A JSON Schema per message** (the message's shape) plus catalog metadata — key, display name,
   category, default channel, and an example payload. The natural carrier is a small **manifest**
   that lists messages and points at their schemas/examples.
2. **A `JsonToRedisValue` adapter** — a single function `string json → RedisValue` (equivalently
   `byte[]`) that produces the bytes to publish. No `IPulsarPlugin`, no `IMessageCatalog`, no
   `IMessageSerializer`.

The publish path collapses to:

```
[ JSON in the editor ] ─► (optional: validate against schema, advisory) ─► JsonToRedisValue(json) ─► RedisValue ─► Redis
```

and the template path becomes pure data:

```
manifest example / schema "examples" ─► [ JSON in the editor ]
```

No CLR `MessageType`. No `JSON → object → JSON` bounce. The transport is unchanged —
`IMessageTransport.PublishAsync` already takes raw `byte[]`, and StackExchange.Redis `RedisValue`
is constructed from a `string` or `byte[]`, so the adapter's output drops straight onto the wire.

### What a plugin becomes

Two tiers, by need:

- **Tier 0 — data only (no code).** Ship a manifest + schemas + examples and select a *built‑in*
  adapter by name (`json-passthrough` or `json-envelope`). There is **no assembly to compile or
  load** → no `AssemblyLoadContext`, no type‑identity dance, and **no Smart App Control DLL‑load
  failures** (see `windows-smart-app-control` memory — freshly‑built unsigned plugin DLLs get
  blocked on this machine; a data‑only plugin has nothing to block).
- **Tier 1 — data + a tiny adapter.** When the wire format is non‑trivial (binary, protobuf, a
  bespoke framing), supply one method `RedisValue JsonToRedisValue(string json)` in a small
  assembly. This is the *only* code, and it is far less surface than today's three interfaces.

### Discovery without an interface

The one thing the interface gave us "for free" was discovery ("find the single `IPulsarPlugin`").
Removing it means the **manifest** names the adapter explicitly — clean and inspectable:

```jsonc
// pulsar.plugin.json  (a manifest = catalog-as-data)
{
  "name": "Sample Messages",
  "adapter": "json-envelope",                 // built-in; OR "MyLib.dll!Acme.Wire.Adapter.JsonToRedisValue"
  "messages": [
    {
      "key": "HeartbeatTelemetry",
      "displayName": "Heartbeat",
      "category": "Telemetry",
      "defaultChannel": "telemetry.heartbeat",
      "schema": "schemas/heartbeat.schema.json",
      "example": "examples/heartbeat.json"     // OR rely on the schema's own "examples"/"default"
    }
  ]
}
```

This makes the catalog **versionable, diffable, and hand‑authorable** — and crucially it can be
**generated from contracts the system‑under‑test team already owns** (AsyncAPI/OpenAPI/JSON
Schema), instead of writing a Pulsar‑specific plugin by hand.

---

## 4. The key realization: the adapter subsumes the serializer

This is the crux of "is it better." The current pipeline tail and the proposed adapter are the
same shape; today's is a special case.

**Today's serializer, re‑expressed as a `JsonToRedisValue` adapter** (a back‑compat shim — proves
nothing is lost):

```csharp
// Wrap any existing IMessageSerializer + CLR type as the new one-function adapter.
RedisValue Adapter(string json)
{
    object obj = JsonSerializer.Deserialize(json, descriptor.MessageType, WebOptions)!; // the old Rehydrate
    byte[]  bytes = legacySerializer.Serialize(obj, descriptor);                        // the old Serialize
    return bytes;                                                                       // RedisValue <- byte[]
}
```

Because every current plugin can be mechanically expressed as an adapter, **the proposed model is
a superset**. Adopting it forecloses nothing. What changes is the *default*: instead of forcing a
CLR type and a typed serializer on everyone, the typed path becomes an opt‑in adapter for the
minority who need it.

**The sample plugin's envelope, as a code adapter** (compare `SampleJsonSerializer.cs`):

```csharp
public static RedisValue JsonToRedisValue(string json)   // no IPulsarPlugin, no descriptor needed
{
    var payload = JsonNode.Parse(json);
    var envelope = new JsonObject
    {
        ["messageType"]    = /* manifest key, passed via convention/closure */ "HeartbeatTelemetry",
        ["correlationId"]  = Guid.NewGuid().ToString(),
        ["emittedAtUnixMs"]= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        ["payload"]        = payload,
    };
    return Encoding.UTF8.GetBytes(envelope.ToJsonString());
}
```

**…or, as Tier‑0 data only**, the same envelope is the built‑in `json-envelope` adapter named in the
manifest — **zero lines of plugin code**.

And a JSON Schema replaces the `HeartbeatTelemetry` POCO + `[PublishChannel]` attribute:

```jsonc
// schemas/heartbeat.schema.json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Heartbeat",
  "type": "object",
  "properties": {
    "deviceId":       { "type": "string",  "default": "device-001" },
    "sequenceNumber": { "type": "integer", "minimum": 0 },
    "status":         { "type": "string",  "enum": ["Nominal", "Degraded", "Down"], "default": "Nominal" },
    "uptimeSeconds":  { "type": "number",  "default": 3600 }
  },
  "required": ["deviceId", "status"]
}
```

Note this schema expresses things the POCO **could not**: `status` is constrained to an enum,
`sequenceNumber` has a minimum, fields are explicitly required. That is a richer, more honest
contract for a *testing* tool than a C# class whose only "validation" was silent coercion.

---

## 5. Side‑by‑side

| Dimension | Today: `IPulsarPlugin` + `IMessageSerializer` | Proposed: JSON Schema + `JsonToRedisValue` |
| --- | --- | --- |
| Message shape source | CLR `Type` (POCO) | JSON Schema (data) |
| Catalog | Code (`MessageCatalog` of descriptors) | Data (manifest), generatable from AsyncAPI/OpenAPI |
| Editing boundary | JSON | JSON (unchanged) |
| Wire‑format seam input | typed `object` | `string json` |
| Wire‑format freedom | Full (`byte[]`) | Full (`RedisValue`/`byte[]`) — strict superset |
| JSON‑on‑wire common case | `JSON → object → JSON` round‑trip (redundant) | direct; optional envelope |
| Plugin for plain/enveloped JSON | Must compile an assembly | **Zero code** (data + built‑in adapter) |
| Contract surface to learn | 3 interfaces + descriptor + attribute | 1 function (+ a manifest schema) |
| Validation | Implicit, lenient, lossy (drops/coerces) | Explicit JSON Schema (required/enum/range/format), **advisory** |
| Send malformed on purpose | Hard — round‑trip normalizes it away | Easy — passthrough sends exactly what you typed |
| Template source | `CreateTemplate()` (typed, always realistic) | schema `examples`/manifest (data; can drift) |
| Assembly loading | Collectible `AssemblyLoadContext` always | Only when a code adapter is supplied; none for Tier 0 |
| Smart App Control risk | Every plugin is a fresh unsigned DLL → blocked | Tier 0 has no DLL → unaffected |
| Coupling of message lib to Pulsar | References `Pulsar.Contracts`, implements interface | None (Tier 0) or one delegate (Tier 1) |
| Binary/protobuf/Avro wire format | Typed object in hand | Re‑parse JSON in adapter (modest extra step) |

---

## 6. What you gain / what you give up

### Gains

1. **Removes a redundant round‑trip** for the dominant case (JSON on the wire): no
   `JSON → POCO → JSON`, no field loss/coercion surprises.
2. **Zero‑code plugins** for plain or enveloped JSON — a manifest + schemas, no compile step. Big
   drop in authoring friction and onboarding cost.
3. **Sidesteps the SAC DLL‑load failure** for Tier‑0 plugins (no assembly to be blocked), which is
   a recurring, machine‑specific pain here.
4. **Catalog‑as‑data**: versionable, diffable, reviewable, and **generatable** from contracts the
   downstream team already publishes (AsyncAPI/OpenAPI/JSON Schema).
5. **Richer, explicit validation** available (required/enum/range/pattern/format) — strictly more
   informative than the current implicit coercion, and it can *catch typos* the current round‑trip
   silently swallows.
6. **Smaller contract** (one function vs three interfaces) → lower coupling; a message library need
   not take a Pulsar dependency at all in Tier 0.
7. **"Send exactly what I typed."** For a fault/edge‑case injection tool this is a feature: a
   passthrough adapter publishes the literal bytes, including deliberately invalid payloads.

### Costs / things to get right

1. **The wire seam receives a string, not a typed object.** Non‑JSON formats (protobuf, Avro,
   MessagePack, bespoke binary) must re‑parse the JSON inside the adapter. This is *optional* (only
   those users pay it) and usually a one‑liner via the format's own JSON mapping — but it is real,
   and we should document it with a worked protobuf example.
2. **Validation must be advisory, not enforcing.** A message‑injection tool's whole purpose
   includes sending malformed messages. Make schema validation a *warn‑don't‑block* signal in the
   UI (a badge: "payload doesn't match schema"), with an explicit "send anyway." Never reject.
3. **Template/example authoring.** `CreateTemplate()` could never drift from its type; a separate
   example file can. Mitigate by validating each example against its own schema at load, and by
   offering a "generate skeleton from schema" fallback when no example is given.
4. **New dependency.** A JSON Schema validator (e.g. `JsonSchema.Net` or `NJsonSchema`). Small and
   well‑maintained, but it is a new third‑party surface in `Pulsar.Core`.
5. **Discovery is now explicit.** Without the interface, the manifest must name the adapter
   (`"json-envelope"` or `"Lib.dll!Type.Method"`). Slightly more to specify; much easier to inspect.
6. **Migration.** Existing `IPulsarPlugin` plugins (incl. the sample) need a path. The shim in §4
   makes this trivial — keep both models during transition.

---

## 7. A pragmatic synthesis (recommended shape)

Don't frame this as "replace." Frame it as **generalize the tail and make data the default**:

- **Transport unchanged.** `IMessageTransport.PublishAsync(channel, byte[])` stays.
- **Introduce the adapter as the primary plugin shape.** `delegate RedisValue JsonToRedisValue(string json)`
  (or `byte[]`). Provide built‑ins: `json-passthrough` and `json-envelope`.
- **Catalog becomes a manifest** (`pulsar.plugin.json`): per‑message key, display name, category,
  default channel, schema ref, example ref, and the adapter name.
- **Validation is advisory** — surfaced in the composer, never blocking publish.
- **Keep `IMessageSerializer` as a Tier‑1 adapter** for typed/binary formats. Internally it is just
  an adapter that deserializes to a registered CLR type first (the §4 shim), so the existing sample
  keeps working unchanged behind a back‑compat loader.
- **Load an assembly only when a code adapter is named.** Tier‑0 plugins never touch
  `AssemblyLoadContext`.

This preserves every capability, makes the 80% case code‑free, shrinks the contract, and removes a
class of Windows‑specific load failures — while leaving a clean door open for exotic wire formats.

### Rough surface sketch (illustrative, not final)

```csharp
namespace Pulsar.Contracts;

// The entire required contract for a code adapter — one function.
public delegate RedisValue JsonToRedisValue(string editedJson);

// Catalog entry, now data rather than a CLR-typed descriptor.
public sealed record MessageManifestEntry(
    string Key, string DisplayName, MessageCategory Category,
    string DefaultChannel, string SchemaPath, string? ExamplePath);

public sealed record PluginManifest(
    string Name, string Adapter, IReadOnlyList<MessageManifestEntry> Messages);
```

```
proposed publish path (note: no CLR MessageType anywhere)
─────────────────────────────────────────────────────────
 editor JSON
   │
   ├─► validate(schema)  ──► advisory badge only (never blocks)
   │
   └─► JsonToRedisValue(json)  ──►  RedisValue  ──►  IMessageTransport.PublishAsync(channel, bytes)
        (built-in json-passthrough / json-envelope, or a named code adapter)
```

---

## 8. Worked migration of the sample

| Today (`backend/samples/Pulsar.SampleMessages`) | Proposed |
| --- | --- |
| `SamplePlugin.cs` (scans for `[PublishChannel]`, builds catalog) | `pulsar.plugin.json` manifest (data) |
| `Messages.cs` POCOs + `[PublishChannel]` attribute | one `*.schema.json` per message + an example each |
| `SampleJsonSerializer.cs` (envelope) | built‑in `json-envelope` adapter (**0 lines**) — or the ~10‑line code adapter in §4 if customized |
| `PublishChannelAttribute.cs` | folded into manifest fields (`category`, `defaultChannel`, `key`, `displayName`) |
| Compiled DLL, loaded via `AssemblyLoadContext` | static files; no load context, no SAC exposure |

Net: the sample goes from **4 source files + a build + a DLL load** to **a manifest + N schema files
+ N example files + a built‑in adapter name**, and gains real schema validation in the editor.

---

## 9. Open questions to resolve before committing

1. **Adapter reference syntax** for Tier 1 — `"MyLib.dll!Namespace.Type.Method"` vs a marker
   attribute vs a tiny one‑method interface. (Leaning: assembly‑qualified method ref in the manifest.)
2. **Does the adapter need per‑message context** (the key/channel), as today's `Serialize` receives
   the `descriptor`? If so, the signature becomes `JsonToRedisValue(string json, MessageContext ctx)`.
3. **Schema dialect & validator** — pin a JSON Schema draft (2020‑12) and pick the library.
4. **Where examples come from** — schema `examples`/`default` vs a sibling example file vs generated.
5. **Multi‑message schema files** vs one file per message (manifest can support either).
6. **Back‑compat window** — keep the `IPulsarPlugin` loader indefinitely, or deprecate after the
   sample and docs migrate?

---

## 10. Recommendation

Adopt the JSON‑schema + `JsonToRedisValue` model as Pulsar's **primary** plugin shape, implemented
as a **generalization** of today's pipeline (the current serializer becomes one kind of adapter via
the §4 shim, so nothing regresses). Ship built‑in `json-passthrough` and `json-envelope` adapters so
the common case is **code‑free**, keep schema validation **advisory**, and retain the typed
`IMessageSerializer` path as a Tier‑1 adapter for binary formats.

This is better than what we have for Pulsar's specific purpose: it removes a redundant round‑trip,
makes the catalog data you can generate and diff, drops the authoring burden (and the Smart App
Control DLL‑load failures) for the majority of plugins, and gives the editor real validation —
all without giving up the full wire‑format freedom that is Pulsar's core value proposition.

If we decide to proceed, the next step is to capture this as an ADR (the decision record) and a
short L2 requirements pass for the manifest format, the built‑in adapters, and the advisory
validation UX.
