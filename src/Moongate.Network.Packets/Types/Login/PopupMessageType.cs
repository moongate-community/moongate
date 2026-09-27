namespace Moongate.Network.Packets.Types.Login;

/// <summary>
///     The built-in popups the client shows for packet 0x53; the byte selects the text.
/// </summary>
public enum PopupMessageType : byte
{
    /// <summary>
    ///     "Incorrect name/password."
    /// </summary>
    IncorrectPassword = 0x00,

    /// <summary>
    ///     "This character does not exist any more!"
    /// </summary>
    CharacterDoesNotExist = 0x01,

    /// <summary>
    ///     "This character already exists."
    /// </summary>
    CharacterExists = 0x02,

    /// <summary>
    ///     "Could not attach to game server."
    /// </summary>
    CouldNotAttach = 0x03,

    /// <summary>
    ///     "A character is already logged in."
    /// </summary>
    CharacterInWorld = 0x05,

    /// <summary>
    ///     "Login sync error."
    /// </summary>
    LoginSyncError = 0x06,

    /// <summary>
    ///     "You have been idle for too long."
    /// </summary>
    IdleWarning = 0x07
}
