namespace Moongate.UO.Data.Json.Teleporters;

/// <summary>
/// Represents one teleporter endpoint payload in ModernUO teleporters.json.
/// </summary>
public class JsonTeleporterEndpoint
{
    public string Map { get; set; } = string.Empty;

    public int[] Loc { get; set; } = [];
}
