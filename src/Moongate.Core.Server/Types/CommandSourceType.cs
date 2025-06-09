namespace Moongate.Core.Server.Types;

[Flags]
public enum CommandSourceType : byte
{
    Console,
    InGame,
    All = Console | InGame,
}
