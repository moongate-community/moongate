namespace Moongate.UO.Data.Teleporters;

/// <summary>
/// A teleporter linking a source location to a destination; <see cref="Back" /> marks a return pad.
/// </summary>
public sealed class TeleporterDefinition
{
    public TeleporterEndpoint Src { get; set; } = new();
    public TeleporterEndpoint Dst { get; set; } = new();
    public bool Back { get; set; }
}
