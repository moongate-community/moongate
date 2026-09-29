namespace Moongate.Server.Ultima.Types.Characters;

/// <summary>
///     Why a character could not be deleted, as packet 0x85 tells the client; the byte selects the client's text.
/// </summary>
public enum CharacterDeleteResultType : byte
{
    /// <summary>
    ///     The password is not correct.
    /// </summary>
    IncorrectPassword = 0x00,

    /// <summary>
    ///     The character does not exist.
    /// </summary>
    CharacterDoesNotExist = 0x01,

    /// <summary>
    ///     The character is being played right now.
    /// </summary>
    CharacterBeingPlayed = 0x02,

    /// <summary>
    ///     The character is not old enough to delete.
    /// </summary>
    CharacterNotOldEnough = 0x03,

    /// <summary>
    ///     The character is queued for backup and cannot be deleted.
    /// </summary>
    CharacterQueuedForBackup = 0x04,

    /// <summary>
    ///     The request could not be carried out.
    /// </summary>
    RequestFailed = 0x05
}
