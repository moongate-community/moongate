using System.Diagnostics.CodeAnalysis;

namespace Moongate.Server.Ultima.Types.Characters;

/// <summary>
///     Flags sent at the end of the character list packet (0xA9). They are not the feature flags of packet 0xB9:
///     the bit layout is different.
/// </summary>
[Flags, SuppressMessage(
     "Design",
     "CA1069:Enums values should not be duplicated",
     Justification = "Each expansion has its own name even when it adds no character list flags."
 )]
public enum CharacterListFlags : uint
{
    None = 0x00000000,
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
    SeventhCharacterSlot = 0x00001000,
    NewMovementSystem = 0x00004000,
    NewFeluccaAreas = 0x00008000,

    ExpansionNone = ContextMenus,
    ExpansionT2A = ContextMenus,
    ExpansionUor = ContextMenus,
    ExpansionUotd = ContextMenus,
    ExpansionLbr = ContextMenus,
    ExpansionAos = ContextMenus | Aos,
    ExpansionSe = ExpansionAos | Se,
    ExpansionMl = ExpansionSe | Ml,
    ExpansionSa = ExpansionMl,
    ExpansionHs = ExpansionSa,
    ExpansionTol = ExpansionHs,
    ExpansionEj = ExpansionTol,

    /// <summary>
    ///     The flags of the newest expansion, the only one the server targets. Slot flags are added by the caller.
    /// </summary>
    Default = ExpansionEj
}
