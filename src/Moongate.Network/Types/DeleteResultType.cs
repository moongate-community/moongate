namespace Moongate.Network.Types;

/// <summary>Why a character deletion (0x83) was refused, as carried by the delete result packet (0x85).</summary>
public enum DeleteResultType : byte
{
    PasswordInvalid = 0x00,
    CharNotExist = 0x01,
    CharBeingPlayed = 0x02,
    CharTooYoung = 0x03,
    CharQueued = 0x04,
    BadRequest = 0x05
}
