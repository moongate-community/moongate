namespace Moongate.Server.Http.Data;

/// <summary>
/// Read-only account payload returned by the player portal.
/// </summary>
public sealed class MoongateHttpPortalAccount
{
    public required string AccountId { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string AccountType { get; init; }

    public required IReadOnlyList<MoongateHttpPortalCharacter> Characters { get; init; }
}
