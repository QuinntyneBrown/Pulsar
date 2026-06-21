namespace Pulsar.Cli.Authoring.Importers;

/// <summary>Options shared by every importer: the name and wire-adapter for the plugin it produces.</summary>
public sealed record ImportOptions(string PluginName, string Adapter);

/// <summary>
/// Converts an external contract (a JSON Schema, an OpenAPI document, an AsyncAPI
/// document) into a format-agnostic <see cref="PluginDraft"/>. One implementation per
/// format, each in its own file (SRP); the <c>import</c> command exposes each as a
/// subcommand named by <see cref="Format"/> (OCP — adding a format adds no code to the
/// command).
/// </summary>
public interface ISpecImporter
{
    /// <summary>The subcommand verb this importer is exposed as (e.g. <c>openapi</c>).</summary>
    string Format { get; }

    /// <summary>One-line help describing the source format.</summary>
    string Description { get; }

    /// <summary>Reads the spec and produces a draft. Throws <c>PluginLoadException</c> on malformed input.</summary>
    PluginDraft Import(Stream spec, ImportOptions options);
}
