using Moongate.Ultima.Types;

namespace Moongate.Ultima.Interfaces;

/// <summary>One record of a client file index: where a record sits in the data stream and how to read it.</summary>
/// <remarks>
/// This is plain access to every field an index entry can carry, across formats. A classic index only
/// fills <see cref="Lookup" />, <see cref="Length" /> and <see cref="Extra" />; the packed format adds the
/// decompressed size, the compression flag and two further extras. Readers interpret the fields, this
/// contract only stores them.
/// </remarks>
public interface IEntry
{
    /// <summary>Gets or sets the offset of the record in the data stream, or a negative value when the record is absent.</summary>
    int Lookup { get; set; }

    /// <summary>Gets or sets the length in bytes of the record as stored, or a negative value when the record is absent.</summary>
    int Length { get; set; }

    /// <summary>Gets or sets format-specific data packed by the file, such as the width and height of an image.</summary>
    int Extra { get; set; }

    /// <summary>
    /// Gets or sets the length in bytes of the record once decompressed; equals <see cref="Length" /> when it is stored
    /// uncompressed.
    /// </summary>
    int DecompressedLength { get; set; }

    /// <summary>Gets or sets the first additional field a packed file stores with the record.</summary>
    int Extra1 { get; set; }

    /// <summary>Gets or sets the second additional field a packed file stores with the record.</summary>
    int Extra2 { get; set; }

    /// <summary>Gets or sets how the record is compressed in the data stream.</summary>
    CompressionFlagType Flag { get; set; }
}
