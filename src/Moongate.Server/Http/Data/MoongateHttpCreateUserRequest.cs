namespace Moongate.Server.Http.Data;

/// <summary>
/// Request payload for creating a new user.
/// </summary>
public sealed class MoongateHttpCreateUserRequest
{
    public required string Username { get; init; }

    public required string Password { get; init; }

    public string Email { get; init; }

    public string Role { get; init; } = "Regular";
}
