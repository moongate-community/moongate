namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The colours of a health bar the client draws (0x17): green for a poisoned mobile, yellow for a blessed one.
/// </summary>
public enum HealthBarType : ushort
{
    Normal = 0,
    Poison = 1,
    Yellow = 2
}
