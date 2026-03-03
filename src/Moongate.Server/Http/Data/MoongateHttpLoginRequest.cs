namespace Moongate.Server.Http.Data;

/// <summary>
/// Login request payload for JWT authentication.
/// </summary>
public sealed class MoongateHttpLoginRequest
{
    public string Username { get; init; }

    public string Password { get; init; }
}
