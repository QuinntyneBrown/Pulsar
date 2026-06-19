using System.Globalization;
using System.Text.Json;

namespace Pulsar.Core.Messages;

/// <summary>Outcome of validating a payload against a schema. Advisory — never blocks a publish.</summary>
public sealed record ValidationResult(bool Matches, IReadOnlyList<string> Messages)
{
    public static readonly ValidationResult Ok = new(true, Array.Empty<string>());
}

/// <summary>
/// A deliberately small, advisory JSON Schema validator. It covers the keywords
/// Pulsar's message schemas actually use — <c>type</c>, <c>required</c>,
/// <c>properties</c>, <c>items</c>, <c>enum</c>, <c>const</c>, numeric bounds,
/// string lengths, and array sizes — and is intentionally NOT a full
/// draft-2020-12 implementation.
/// </summary>
/// <remarks>
/// Its single rule is to <em>warn, never block</em>: Pulsar is a message-injection
/// tool, so the user must always be able to publish a deliberately malformed
/// payload. Accordingly this validator never throws — any internal error degrades to
/// "matches" rather than surfacing as a failure — and the caller is expected to treat
/// the result as a hint, not a gate.
/// </remarks>
public static class SchemaValidator
{
    public static ValidationResult Validate(string payloadJson, CompiledSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        JsonElement instance;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson ?? "");
            instance = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return new ValidationResult(false, new[] { $"Payload is not valid JSON: {ex.Message}" });
        }

        var errors = new List<string>();
        try
        {
            ValidateNode(instance, schema.Root, "$", errors);
        }
        catch
        {
            // Advisory only: a bug or unsupported construct in the validator must never
            // be reported to the user as if their payload were at fault.
            return ValidationResult.Ok;
        }

        return errors.Count == 0 ? ValidationResult.Ok : new ValidationResult(false, errors);
    }

    private static void ValidateNode(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return; // `true`/`false` schemas and non-object schemas: nothing to check here.

        if (schema.TryGetProperty("type", out var typeEl))
            ValidateType(value, typeEl, path, errors);

        if (schema.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
        {
            var matched = enumEl.EnumerateArray().Any(allowed => JsonEquals(value, allowed));
            if (!matched)
                errors.Add($"{path}: value is not one of the allowed enum values.");
        }

        if (schema.TryGetProperty("const", out var constEl) && !JsonEquals(value, constEl))
            errors.Add($"{path}: value does not equal the required constant.");

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(value, schema, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(value, schema, path, errors);
                break;
            case JsonValueKind.String:
                ValidateString(value, schema, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(value, schema, path, errors);
                break;
        }
    }

    private static void ValidateType(JsonElement value, JsonElement typeEl, string path, List<string> errors)
    {
        IEnumerable<string> allowed = typeEl.ValueKind switch
        {
            JsonValueKind.String => new[] { typeEl.GetString()! },
            JsonValueKind.Array => typeEl.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!),
            _ => Array.Empty<string>(),
        };
        allowed = allowed.ToList();
        if (!allowed.Any()) return;

        if (!allowed.Any(t => MatchesType(value, t)))
            errors.Add($"{path}: expected type {string.Join(" or ", allowed)} but got {JsonTypeName(value)}.");
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        "number" => value.ValueKind == JsonValueKind.Number,
        // JSON Schema integer: a number with no fractional part.
        "integer" => value.ValueKind == JsonValueKind.Number
                     && value.TryGetDouble(out var d) && Math.Floor(d) == d,
        _ => true, // unknown type keyword: don't flag.
    };

    private static void ValidateObject(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("required", out var requiredEl) && requiredEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var req in requiredEl.EnumerateArray())
            {
                var name = req.GetString();
                if (name is not null && !value.TryGetProperty(name, out _))
                    errors.Add($"{path}: missing required property '{name}'.");
            }
        }

        if (schema.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsEl.EnumerateObject())
            {
                if (value.TryGetProperty(prop.Name, out var child))
                    ValidateNode(child, prop.Value, $"{path}.{prop.Name}", errors);
            }
        }
    }

    private static void ValidateArray(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        var count = value.GetArrayLength();
        if (schema.TryGetProperty("minItems", out var minEl) && minEl.TryGetInt32(out var min) && count < min)
            errors.Add($"{path}: expected at least {min} item(s) but got {count}.");
        if (schema.TryGetProperty("maxItems", out var maxEl) && maxEl.TryGetInt32(out var max) && count > max)
            errors.Add($"{path}: expected at most {max} item(s) but got {count}.");

        if (schema.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Object)
        {
            var i = 0;
            foreach (var item in value.EnumerateArray())
                ValidateNode(item, itemsEl, $"{path}[{i++}]", errors);
        }
    }

    private static void ValidateString(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        var s = value.GetString() ?? "";
        if (schema.TryGetProperty("minLength", out var minEl) && minEl.TryGetInt32(out var min) && s.Length < min)
            errors.Add($"{path}: string is shorter than minLength {min}.");
        if (schema.TryGetProperty("maxLength", out var maxEl) && maxEl.TryGetInt32(out var max) && s.Length > max)
            errors.Add($"{path}: string is longer than maxLength {max}.");
    }

    private static void ValidateNumber(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (!value.TryGetDouble(out var n)) return;
        if (schema.TryGetProperty("minimum", out var minEl) && minEl.TryGetDouble(out var min) && n < min)
            errors.Add($"{path}: {Fmt(n)} is below minimum {Fmt(min)}.");
        if (schema.TryGetProperty("maximum", out var maxEl) && maxEl.TryGetDouble(out var max) && n > max)
            errors.Add($"{path}: {Fmt(n)} is above maximum {Fmt(max)}.");
        if (schema.TryGetProperty("exclusiveMinimum", out var exMinEl) && exMinEl.TryGetDouble(out var exMin) && n <= exMin)
            errors.Add($"{path}: {Fmt(n)} is not greater than exclusiveMinimum {Fmt(exMin)}.");
        if (schema.TryGetProperty("exclusiveMaximum", out var exMaxEl) && exMaxEl.TryGetDouble(out var exMax) && n >= exMax)
            errors.Add($"{path}: {Fmt(n)} is not less than exclusiveMaximum {Fmt(exMax)}.");
    }

    private static string Fmt(double d) => d.ToString(CultureInfo.InvariantCulture);

    private static string JsonTypeName(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => "undefined",
    };

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Treat true/false as the same kind family for comparison.
            var aBool = a.ValueKind is JsonValueKind.True or JsonValueKind.False;
            var bBool = b.ValueKind is JsonValueKind.True or JsonValueKind.False;
            if (!(aBool && bBool)) return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.String:
                return a.GetString() == b.GetString();
            case JsonValueKind.Number:
                return a.TryGetDouble(out var ad) && b.TryGetDouble(out var bd) && ad == bd;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.ValueKind == b.ValueKind;
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength()) return false;
                return a.EnumerateArray().Zip(b.EnumerateArray(), JsonEquals).All(x => x);
            case JsonValueKind.Object:
                var aProps = a.EnumerateObject().OrderBy(p => p.Name).ToList();
                var bProps = b.EnumerateObject().OrderBy(p => p.Name).ToList();
                if (aProps.Count != bProps.Count) return false;
                return aProps.Zip(bProps, (x, y) => x.Name == y.Name && JsonEquals(x.Value, y.Value)).All(x => x);
            default:
                return false;
        }
    }
}
