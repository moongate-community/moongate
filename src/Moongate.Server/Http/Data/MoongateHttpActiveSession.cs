namespace Moongate.Server.Http.Data;

/// <summary>
/// Active in-game session payload.
/// </summary>
public sealed class MoongateHttpActiveSession
{
    public required long SessionId { get; init; }

    public required string AccountId { get; init; }

    public required string Username { get; init; }

    public required string AccountType { get; init; }

    public required string CharacterId { get; init; }

    public required string CharacterName { get; init; }
}
