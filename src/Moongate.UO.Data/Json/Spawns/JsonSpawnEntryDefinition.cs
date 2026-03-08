namespace Moongate.UO.Data.Json.Spawns;

/// <summary>
/// Represents one weighted entry inside a ModernUO spawner JSON object.
/// </summary>
public class JsonSpawnEntryDefinition
{
    public string Name { get; set; } = string.Empty;

    public int MaxCount { get; set; }

    public int Probability { get; set; }
}
