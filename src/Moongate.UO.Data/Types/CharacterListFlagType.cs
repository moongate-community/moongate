namespace Moongate.UO.Data.Types;

/// <summary>Character-list feature flags (0xA9): the client capabilities advertised at login.</summary>
[Flags]
public enum CharacterListFlagType
{
    None = 0x00000000,
    Unknown1 = 0x00000001,
    OverwriteConfigButton = 0x00000002,
    OneCharacterSlot = 0x00000004,
    ContextMenus = 0x00000008,
    SlotLimit = 0x00000010,
    Aos = 0x00000020,
    SixthCharacterSlot = 0x00000040,
    Se = 0x00000080,
    Ml = 0x00000100,
    Kr = 0x00000200,
    Uo3DClientType = 0x00000400,
    Unknown3 = 0x00000800,
    SeventhCharacterSlot = 0x00001000,
    Unknown4 = 0x00002000,
    NewMovementSystem = 0x00004000,
    NewFeluccaAreas = 0x00008000,

    /// <summary>Constant modern (7.x) feature set enabling seven character slots.</summary>
    Modern = ContextMenus | Aos | Se | Ml | SixthCharacterSlot | SeventhCharacterSlot
}
