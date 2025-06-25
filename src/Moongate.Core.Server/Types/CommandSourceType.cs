namespace Moongate.Core.Server.Types;

[Flags]
public enum CommandSourceType : byte
{
    None = 0x00,
    Console = 1,
    InGame = 2,
    All = Console | InGame
}
