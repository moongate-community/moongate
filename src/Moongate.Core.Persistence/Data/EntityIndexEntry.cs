namespace Moongate.Core.Persistence.Data;

/// <summary>
/// Index entry for entity location in file
/// </summary>
public struct EntityIndexEntry
{
    /// <summary>
    /// Hash of the serialized entity data
    /// </summary>
    public ulong DataHash { get; set; }

    /// <summary>
    /// Offset in file where entity data starts
    /// </summary>
    public ulong Offset { get; set; }

    public EntityIndexEntry(ulong dataHash, ulong offset)
    {
        DataHash = dataHash;
        Offset = offset;
    }
}
