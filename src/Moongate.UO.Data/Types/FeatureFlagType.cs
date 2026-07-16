namespace Moongate.UO.Data.Types;

/// <summary>Client feature flags (0xB9): the locked client features unlocked at login.</summary>
[Flags]
public enum FeatureFlagType
{
    None = 0x00000000,
    T2A = 0x00000001,
    Uor = 0x00000002,
    Uotd = 0x00000004,
    Lbr = 0x00000008,
    Aos = 0x00000010,
    SixthCharacterSlot = 0x00000020,
    Se = 0x00000040,
    Ml = 0x00000080,
    EighthAge = 0x00000100,
    NinthAge = 0x00000200,
    TenthAge = 0x00000400,
    IncreasedStorage = 0x00000800,
    SeventhCharacterSlot = 0x00001000,
    RoleplayFaces = 0x00002000,
    TrialAccount = 0x00004000,
    LiveAccount = 0x00008000,
    Sa = 0x00010000,
    Hs = 0x00020000,
    Gothic = 0x00040000,
    Rustic = 0x00080000,
    Jungle = 0x00100000,
    Shadowguard = 0x00200000,
    Tol = 0x00400000,
    Ej = 0x00800000,

    /// <summary>
    /// Constant modern (7.x, Endless Journey) feature set with six and seven character slots
    /// enabled. Moongate targets modern clients only, so this is the fixed set advertised at login.
    /// </summary>
    Modern = Lbr |
             Aos |
             LiveAccount |
             Se |
             Ml |
             NinthAge |
             Sa |
             Gothic |
             Rustic |
             Hs |
             Tol |
             Jungle |
             Shadowguard |
             Ej |
             SixthCharacterSlot |
             SeventhCharacterSlot
}
