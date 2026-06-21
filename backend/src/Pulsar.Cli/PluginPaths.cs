namespace Pulsar.Cli;

/// <summary>Path conventions shared by the verbs that consume a plugin.</summary>
public static class PluginPaths
{
    /// <summary>
    /// If <paramref name="path"/> is a directory, resolves it to the
    /// <c>pulsar.plugin.json</c> inside it; otherwise returns it unchanged (a manifest
    /// file or a legacy <c>.dll</c>).
    /// </summary>
    public static string Resolve(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? Path.Combine(path, "pulsar.plugin.json")
            : path;
}
