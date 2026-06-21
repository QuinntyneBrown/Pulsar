using System.CommandLine;

namespace Pulsar.Cli.Abstractions;

/// <summary>
/// One CLI verb. Each implementation lives in its own file (SRP) and builds the
/// <see cref="Command"/> it owns — options, arguments, and handler. The composition
/// root discovers every registered <see cref="ICliCommand"/> and adds it to the root
/// command, so adding a verb never edits the root assembly (OCP).
/// </summary>
public interface ICliCommand
{
    /// <summary>Builds the fully-wired System.CommandLine command for this verb.</summary>
    Command Build();
}
