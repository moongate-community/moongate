namespace Moongate.UO.Data.Types;

/// <summary>
/// Represents GetPlayerStatusType.
/// </summary>
public enum GetPlayerStatusType : byte
{
    GodClient = 0x00,
    BasicStatus = 0x04,  // Packet 0x11
    RequestSkills = 0x05 // Packet 0x3A
}
