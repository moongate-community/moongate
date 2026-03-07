namespace Moongate.UO.Data.Json.Spawns;

/// <summary>
/// Represents one spawner JSON object from ModernUO distribution data.
/// </summary>
public class JsonSpawnDefinition
{
    public string Type { get; set; } = string.Empty;

    public Guid Guid { get; set; }

    public string Name { get; set; } = string.Empty;

    public int[] Location { get; set; } = [];

    public string Map { get; set; } = string.Empty;

    public int Count { get; set; }

    public TimeSpan MinDelay { get; set; }

    public TimeSpan MaxDelay { get; set; }

    public int Team { get; set; }

    public int HomeRange { get; set; }

    public int WalkingRange { get; set; }

    public List<JsonSpawnEntryDefinition> Entries { get; set; } = [];
}
