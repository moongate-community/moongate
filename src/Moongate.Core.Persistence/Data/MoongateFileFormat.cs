using System.Text;

namespace Moongate.Core.Persistence.Data;

// <summary>
/// Binary file format for Moongate entities
/// Format:
/// [Header: "MOONGATE"] - 8 bytes
/// [Version] - 4 bytes
/// [Entity Count] - 4 bytes
/// [Index Table] - N * 16 bytes (each entry: Type Hash + Offset)
/// [Entity Data] - Variable length (Type + Length + Serialized Data)
/// </summary>
public class MoongateFileFormat
{
    /// <summary>
    /// File header magic bytes
    /// </summary>
    public static readonly byte[] HEADER_MAGIC = Encoding.ASCII.GetBytes("MOONGATE");

    /// <summary>
    /// Current file format version
    /// </summary>
    public const uint CURRENT_VERSION = 1;

    /// <summary>
    /// Size of each index entry in bytes
    /// </summary>
    public const int INDEX_ENTRY_SIZE = 16; // 8 bytes type hash + 8 bytes offset
}
