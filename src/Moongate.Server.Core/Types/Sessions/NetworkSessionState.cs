namespace Moongate.Server.Core.Types.Sessions;

public enum NetworkSessionState
{
    Connected = 0,
    AwaitingSeed = 1,
    Login = 2,
    Authenticated = 3,
    InGame = 4,
    Disconnecting = 5,
    Disconnected = 6
}
