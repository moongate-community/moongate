namespace Moongate.Server.Data.World;

/// <summary>
/// Groups public moongate destinations under a shared shard-facing category.
/// </summary>
public sealed record PublicMoongateGroupDefinition(
    string Id,
    string Name,
    IReadOnlyList<PublicMoongateDestinationDefinition> Destinations
);
