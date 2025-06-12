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

    public EntityDataBlock(string typeName, byte[] data)
    {
        TypeName = typeName;
        DataLength = (uint)data.Length;
        Data = data;
    }
}
