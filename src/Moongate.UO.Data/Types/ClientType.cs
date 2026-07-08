namespace Moongate.UO.Data.Types;

/// <summary>
/// The Ultima Online client family a connection belongs to: the 2D classic client, the
/// Third Dawn client, the Korean (KR) client or the Stygian Abyss (SA) enhanced client.
/// </summary>
[Flags]
public enum ClientType
{
    None = 0x00,
    Classic = 0x01,
    UOTD = 0x02,
    KR = 0x04,
    SA = 0x08
}
