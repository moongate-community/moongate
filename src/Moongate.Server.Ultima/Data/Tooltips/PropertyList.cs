using System.Buffers.Binary;
using System.Text;

namespace Moongate.Server.Ultima.Data.Tooltips;

/// <summary>
///     The lines of an AOS tooltip (object property list), as ModernUO builds it, and the hash that tells the client
///     whether the tooltip it has is still current.
/// </summary>
public sealed class PropertyList
{
    // A longer argument overflows the older clients' buffer and crashes them (ModernUO).
    public const int MaxArgumentLength = 504;

    // Clilocs whose whole text is ~1_NOTHING~ or ~1_val~: the client shows each number once, so free texts rotate.
    private static readonly int[] TextClilocs = [1042971, 1070722, 1114057, 1114778, 1114779];

    private readonly List<PropertyEntry> _entries = [];
    private int _texts;

    public IReadOnlyList<PropertyEntry> Entries => _entries;

    /// <summary>
    ///     Gets the hash of the lines, 26 bits, as ModernUO masks it: equal lines give an equal hash.
    /// </summary>
    public int Hash => ComputeHash();

    public void Add(int cliloc)
    {
        Add(cliloc, "");
    }

    public void Add(int cliloc, string arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        _entries.Add(new(cliloc, arguments.Length > MaxArgumentLength ? arguments[..MaxArgumentLength] : arguments));
    }

    /// <summary>
    ///     Adds a free text line through the next empty cliloc; after five, the first is reused.
    /// </summary>
    public void AddText(string text)
    {
        Add(TextClilocs[_texts++ % TextClilocs.Length], text);
    }

    // FNV-1a over what the packet writes for the lines: cliloc, argument length and UTF-16 arguments.
    private int ComputeHash()
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        Span<byte> number = stackalloc byte[4];

        foreach (var entry in _entries)
        {
            BinaryPrimitives.WriteInt32BigEndian(number, entry.Cliloc);

            foreach (var value in number)
            {
                hash = (hash ^ value) * prime;
            }

            var arguments = Encoding.Unicode.GetBytes(entry.Arguments);
            hash = (hash ^ (uint)(arguments.Length >> 8)) * prime;
            hash = (hash ^ (uint)(arguments.Length & 0xFF)) * prime;

            foreach (var value in arguments)
            {
                hash = (hash ^ value) * prime;
            }
        }

        return (int)(hash & 0x3FFFFFF);
    }
}
