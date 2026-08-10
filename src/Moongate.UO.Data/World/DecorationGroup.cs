namespace Moongate.UO.Data.World;

/// <summary>
/// One decoration declaration and every spot it stands in, as the YAML holds it. The grouping is the
/// one the source .cfg files already had: 8,202 groups instead of 41,013 flat entries, which reads
/// better and stores smaller for exactly the same world.
/// </summary>
public sealed class DecorationGroup
{
    /// <summary>
    /// What the source called it — <c>Static</c> for most, otherwise a RunUO class name such as
    /// <c>MetalDoor</c>. Carried so a later pass can find the ones that need behaviour, rather than
    /// having to re-read the source data to know which those were.
    /// </summary>
    public string Type { get; set; } = "Static";

    /// <summary>The graphic, which is what the client actually draws.</summary>
    public int ItemId { get; set; }

    /// <summary>0 for the raw art.</summary>
    public int Hue { get; set; }

    /// <summary>Each entry is an [x, y, z] triple. Z is signed and often deeply negative.</summary>
    public List<int[]> At { get; set; } = [];
}
