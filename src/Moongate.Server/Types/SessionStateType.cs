namespace Moongate.Server.Types;

/// <summary>Protocol phase of a connected client session.</summary>
public enum SessionStateType : byte
{
    AwaitingSeed = 0,
    Login = 1,
    Authenticated = 2
}
