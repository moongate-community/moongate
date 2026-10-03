namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     What the client asks about a mobile in a mobile query (0x34).
/// </summary>
public enum MobileQueryType : byte
{
    Status = 4,
    Skills = 5
}
