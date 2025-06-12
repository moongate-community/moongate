namespace Moongate.Core.Persistence.Data;

/// <summary>
/// Entity data block in file
/// </summary>
public struct EntityDataBlock
{
    /// <summary>
    /// Type name for deserialization
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Length of serialized data
    /// </summary>
    public uint DataLength { get; set; }

    /// <summary>
    /// Serialized entity data
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// Hash of the serialized data for uniqueness
    /// </summary>
    public ulong DataHash { get; set; }

    public EntityDataBlock(string typeName, byte[] data)
    {
        TypeName = typeName;
        DataLength = (uint)data.Length;
        Data = data;
        DataHash = ComputeDataHash(data);
    }

    /// <summary>
    /// Compute hash from serialized data bytes
    /// </summary>
    private static ulong ComputeDataHash(byte[] data)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(data);

        var hash = BitConverter.ToUInt64(hashBytes, 0);

        return hash;
    }
}
