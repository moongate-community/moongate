using Moongate.Ultima.Io;

namespace Moongate.Ultima.Interfaces;

/// <summary>
///     Reads one Ultima Online client file through its index.
/// </summary>
/// <remarks>
///     A client file is a data stream plus an index that locates each record in it. This contract covers
///     both the classic
///     <c>
///         .mul
///     </c>
///     file paired with an
///     <c>
///         .idx
///     </c>
///     file and the packed
///     <c>
///         .uop
///     </c>
///     format,
///     so readers of art, sounds, maps and the like need not know which one is on disk.
/// </remarks>
public interface IFileAccessor
{
    /// <summary>
    ///     Gets or sets the open stream over the data file that the index entries point into.
    /// </summary>
    FileStream Stream { get; set; }

    /// <summary>
    ///     Gets the number of entries in the index.
    /// </summary>
    int IndexLength { get; }

    /// <summary>
    ///     Gets the size in bytes of the index as it was read from disk.
    /// </summary>
    long IdxLength { get; }

    /// <summary>
    ///     Gets or sets the entry at the given index.
    /// </summary>
    /// <param name="index">
    ///     Zero-based position in the index.
    /// </param>
    /// <exception cref="IndexOutOfRangeException">
    ///     The position is outside the index.
    /// </exception>
    IEntry this[int index] { get; set; }

    /// <summary>
    ///     Overrides one entry with a patch from the client's verdata file.
    /// </summary>
    /// <param name="patch">
    ///     The replacement lookup, length and extra data for the entry it names.
    /// </param>
    /// <remarks>
    ///     The entry is marked as patched, so later reads know its data does not come from the base file.
    /// </remarks>
    void ApplyPatch(Entry5D patch);

    /// <summary>
    ///     Gets the entry at the given index, or an empty entry when the index is out of range.
    /// </summary>
    /// <param name="index">
    ///     Zero-based position in the index.
    /// </param>
    /// <returns>
    ///     The entry, or an empty one whose lookup points nowhere.
    /// </returns>
    /// <remarks>
    ///     Unlike the indexer, this never throws, so callers can probe an id they are not sure exists.
    /// </remarks>
    IEntry GetEntry(int index);
}
