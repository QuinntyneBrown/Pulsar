namespace Pulsar.Contracts;

/// <summary>
/// The per-message context handed to a <see cref="JsonToRedisValue"/> adapter
/// alongside the edited JSON. It carries just enough for an adapter to stamp an
/// envelope (the message <see cref="Key"/>), record where it is going
/// (<see cref="Channel"/>), and branch on the kind of message
/// (<see cref="Category"/>) — without the adapter needing any CLR type.
/// </summary>
public sealed record MessageContext(string Key, string Channel, MessageCategory Category);
