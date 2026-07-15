namespace Moongate.Network.Types;

/// <summary>
/// Sub-commands multiplexed under the general information packet (0xBF). The client picks one via the
/// leading ushort; the list mirrors the client-to-server sub-commands recognised by ModernUO and UOX3.
/// Only a few are acted on today; the rest are named here so future systems can slot in a handler case.
/// </summary>
public enum GeneralInformationSubCommandType : ushort
{
    ScreenSize = 0x05,
    PartyCommand = 0x06,
    TrackingArrow = 0x07,
    DisarmRequest = 0x09,
    WrestlingStun = 0x0A,
    ClientLanguage = 0x0B,
    CloseStatusGump = 0x0C,
    Animate = 0x0E,
    LoginNotice = 0x0F,
    QueryProperties = 0x10,
    ContextMenuRequest = 0x13,
    ContextMenuSelect = 0x15,
    ExtendedStats = 0x1A,
    CastSpell = 0x1C,
    ClientType = 0x24,
    BandageMacro = 0x2C,
    ToggleFlying = 0x32
}
