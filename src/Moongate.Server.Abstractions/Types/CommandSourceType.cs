namespace Moongate.Server.Abstractions.Types;

/// <summary>
/// Where a command invocation originated. Only <see cref="InGame" /> is implemented today — the
/// <c>[Flags]</c> shape leaves room for a future console/REPL source without breaking existing
/// <see cref="Attributes.CommandAttribute" /> declarations.
/// </summary>
[Flags]
public enum CommandSourceType : byte
{
    InGame = 1 << 0
}
