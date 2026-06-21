using System.Text.RegularExpressions;
using Pulsar.Contracts;

namespace Pulsar.Cli.Authoring;

/// <summary>
/// Small, shared naming conventions for generated plugins: turning a message key or
/// title into a file-system slug, a sensible default channel, and a best-effort
/// category. Kept in one place so <c>new</c> and every importer name things alike.
/// </summary>
public static partial class Naming
{
    /// <summary>kebab-cases a value for use as a file name (splits camelCase first).</summary>
    public static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "message";
        var spaced = CamelBoundary().Replace(value.Trim(), "-");
        var slug = NonAlphanumeric().Replace(spaced.ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "message" : slug;
    }

    /// <summary>A reasonable default Redis channel: <c>{prefix}.{slug}</c> from the category.</summary>
    public static string DefaultChannel(string key, MessageCategory category)
    {
        var prefix = category switch
        {
            MessageCategory.Telemetry => "telemetry",
            MessageCategory.Event => "events",
            MessageCategory.Fault => "faults",
            MessageCategory.Command => "commands",
            _ => "messages",
        };
        return $"{prefix}.{Slug(key)}";
    }

    /// <summary>Best-effort category from any naming hint (channel, key, or title).</summary>
    public static MessageCategory Categorize(string? hint)
    {
        var h = (hint ?? string.Empty).ToLowerInvariant();
        if (h.Contains("fault") || h.Contains("error")) return MessageCategory.Fault;
        if (h.Contains("command") || h.Contains("cmd")) return MessageCategory.Command;
        if (h.Contains("telemetry") || h.Contains("metric") || h.Contains("reading")) return MessageCategory.Telemetry;
        if (h.Contains("event") || h.Contains("alert") || h.Contains("notif")) return MessageCategory.Event;
        return MessageCategory.Other;
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
