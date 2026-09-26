namespace Moongate.Server.Core.Types.Commands;

/// <summary>
///     Identifies where a command was submitted from.
/// </summary>
[Flags]
public enum CommandSourceType
{
    /// <summary>
    ///     No source. Never satisfies a command's source gate.
    /// </summary>
    None = 0,
    InGame = 1 << 0,

    /// <summary>
    ///     Asserts administrator authority with no authentication. Only trusted in-process callers may use this source.
    /// </summary>
    Console = 1 << 1
}
