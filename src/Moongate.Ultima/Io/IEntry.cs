using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

// Dumb access to all possible fields of entries
public interface IEntry
{
    int Lookup { get; set; }
    int Length { get; set; }
    int Extra { get; set; }
    int DecompressedLength { get; set; }
    int Extra1 { get; set; }
    int Extra2 { get; set; }
    CompressionFlag Flag { get; set; }
}
