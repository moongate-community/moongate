namespace Moongate.Ultima.Types;

/// <summary>
///     What the client says it can show, sent at character creation: mostly the maps it has installed.
/// </summary>
[Flags]
public enum ClientFlags : uint
{
    None = 0x00000000,
    Felucca = 0x00000001,
    Trammel = 0x00000002,
    Ilshenar = 0x00000004,
    Malas = 0x00000008,
    Tokuno = 0x00000010,
    TerMur = 0x00000020,
    Kr = 0x00000040,
    Uotd = 0x00000100
}
