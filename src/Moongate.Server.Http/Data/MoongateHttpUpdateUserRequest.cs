namespace Moongate.Server.Http.Data;

/// <summary>
/// Request payload for updating an existing user.
/// </summary>
public sealed class MoongateHttpUpdateUserRequest
{
    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? Email { get; init; }

    public string? Role { get; init; }

    public bool? IsLocked { get; init; }
}
