using System.CommandLine;
using System.CommandLine.IO;

namespace Pulsar.Cli.Rendering;

/// <summary>
/// The one place that writes to the console, so commands stay free of raw stream
/// handling and tests can capture output through a <c>TestConsole</c>. Command
/// <em>output</em> goes to stdout; diagnostics and warnings go to stderr.
/// </summary>
public static class ConsoleReport
{
    /// <summary>Writes a line to stdout (the command's output stream).</summary>
    public static void Line(IConsole console, string text = "") => console.Out.WriteLine(text);

    /// <summary>Writes a line to stderr (diagnostics — does not pollute piped output).</summary>
    public static void Error(IConsole console, string text) => console.Error.WriteLine(text);

    /// <summary>Writes a warning to stderr, prefixed for scannability.</summary>
    public static void Warn(IConsole console, string text) => console.Error.WriteLine($"warning: {text}");

    /// <summary>Renders a simple two-or-more column table to stdout, padding each column.</summary>
    public static void Table(IConsole console, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var widths = new int[headers.Count];
        for (var c = 0; c < headers.Count; c++)
        {
            widths[c] = headers[c].Length;
            foreach (var row in rows)
                if (c < row.Length) widths[c] = Math.Max(widths[c], row[c]?.Length ?? 0);
        }

        Line(console, Row(headers.ToArray(), widths));
        Line(console, Row(widths.Select(w => new string('-', w)).ToArray(), widths));
        foreach (var row in rows)
            Line(console, Row(row, widths));
    }

    private static string Row(string[] cells, int[] widths)
    {
        var parts = new string[widths.Length];
        for (var c = 0; c < widths.Length; c++)
        {
            var value = c < cells.Length ? cells[c] ?? "" : "";
            parts[c] = value.PadRight(widths[c]);
        }
        return string.Join("  ", parts).TrimEnd();
    }
}
