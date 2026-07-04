using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

// Dumb access to all possible fields of entries
public interface IEntry
{
    public int Lookup { get; set; }
    public int Length { get; set; }
    public int Extra { get; set; }
    public int DecompressedLength { get; set; }
    public int Extra1 { get; set; }
    public int Extra2 { get; set; }
    public CompressionFlag Flag { get; set; }
}
