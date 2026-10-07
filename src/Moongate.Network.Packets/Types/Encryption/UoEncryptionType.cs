namespace Moongate.Network.Packets.Types.Encryption;

/// <summary>
///     Wire cipher families selected by POL client versions.
/// </summary>
public enum UoEncryptionType
{
    None,
    OldBlowfish,
    Blowfish12536,
    Blowfish,
    BlowfishTwofish,
    Twofish
}
