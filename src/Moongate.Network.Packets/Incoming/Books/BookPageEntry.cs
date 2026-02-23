namespace Moongate.Network.Packets.Incoming.Books;

/// <summary>
/// Represents BookPageEntry.
/// </summary>
public class BookPageEntry
{
    public ushort PageNumber { get; set; }

    public ushort LineCount { get; set; }

    public List<string> Lines { get; }

    public bool IsPageRequest => LineCount == ushort.MaxValue;

    public BookPageEntry()
        => Lines = new();
}
