namespace Moongate.Server.Core.Types.Hosting;

/// <summary>
///     Identifies the server roles selected in configuration.
/// </summary>
[Flags]
public enum ServerMode
{
    /// <summary>
    ///     No role is selected; this is not a valid server configuration.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The login server role.
    /// </summary>
    Login = 1,

    /// <summary>
    ///     The game server role for one realm.
    /// </summary>
    Game = 2,

    /// <summary>
    ///     Both login and game server roles in one process.
    /// </summary>
    Standalone = Login | Game
}
