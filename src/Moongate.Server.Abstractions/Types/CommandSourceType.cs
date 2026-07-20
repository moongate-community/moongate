namespace Moongate.Server.Abstractions.Types;

/// <summary>
/// Where a command invocation originated. The <c>[Flags]</c> shape lets a command opt into more than
/// one source; the dispatcher runs a command only for sources its registration lists.
/// </summary>
[Flags]
public enum CommandSourceType : byte
{
    InGame  = 1 << 0,
    Console = 1 << 1,
    Rest    = 1 << 2
}
