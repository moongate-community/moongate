namespace Moongate.Server.Http.Data;

/// <summary>
/// Read-only character payload returned by the player portal.
/// </summary>
public sealed class MoongateHttpPortalCharacter
{
    public required string CharacterId { get; init; }

    public required string Name { get; init; }

    public required int MapId { get; init; }

    public required string MapName { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }
}
