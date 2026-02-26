namespace Moongate.Server.Http.Data;

/// <summary>
/// User payload returned by HTTP user endpoints.
/// </summary>
public sealed class MoongateHttpUser
{
    public required string AccountId { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Role { get; init; }

    public required bool IsLocked { get; init; }

    public required DateTime CreatedUtc { get; init; }

    public required DateTime LastLoginUtc { get; init; }

    public required int CharacterCount { get; init; }
}
