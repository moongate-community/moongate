namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one entry inside a spawn definition.
/// </summary>
public readonly record struct SpawnEntryDefinition(
    string Name,
    int MaxCount,
    int Probability
);
