namespace Moongate.UO.Data.Json.Teleporters;

/// <summary>
/// Represents one teleporter entry in ModernUO teleporters.json.
/// </summary>
public class JsonTeleporterDefinition
{
    public JsonTeleporterEndpoint Src { get; set; } = new();

    public JsonTeleporterEndpoint Dst { get; set; } = new();

    public bool Back { get; set; }
}
