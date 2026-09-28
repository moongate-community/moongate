namespace Moongate.Server.Ultima.Data.Motd;

/// <summary>
///     A validated MOTD line and its one-based position in the source file.
/// </summary>
public sealed record MotdLine(int Index, string Template);
