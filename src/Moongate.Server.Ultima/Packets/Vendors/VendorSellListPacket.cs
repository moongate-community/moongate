using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     What a vendor offers to buy from a player (0x9E): the vendor, then for each item its serial, graphic, hue,
///     amount, the price of a piece and its name.
/// </summary>
[PacketHandler(0x9E, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Vendor: sell list")]
public sealed class VendorSellListPacket : BasePacket<VendorSellListPacket>, IOutgoingPacket
{
    private const int HeaderLength = 9;
    private const int EntryOverhead = 14;
    private const int NameMaximum = 60;

    private readonly VendorSellListEntry[] _entries;
    private readonly byte[][] _names;

    public override int Length { get; }

    public Serial Vendor { get; }

    public IReadOnlyList<VendorSellListEntry> Entries => _entries;

    public VendorSellListPacket(Serial vendor, IEnumerable<VendorSellListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Vendor = vendor;
        _entries = entries.ToArray();
        _names = _entries.Select(entry => Encoding.ASCII.GetBytes(Cut(entry.Name))).ToArray();
        Length = HeaderLength + _entries.Length * EntryOverhead + _names.Sum(name => name.Length);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Vendor);
        writer.WriteUInt16BigEndian((ushort)_entries.Length);

        for (var index = 0; index < _entries.Length; index++)
        {
            var entry = _entries[index];
            writer.WriteSerial(entry.Item);
            writer.WriteUInt16BigEndian((ushort)entry.ItemId);
            writer.WriteUInt16BigEndian(entry.Hue);
            writer.WriteUInt16BigEndian((ushort)Math.Min(entry.Amount, ushort.MaxValue));
            writer.WriteUInt16BigEndian((ushort)Math.Min(entry.Price, ushort.MaxValue));
            writer.WriteUInt16BigEndian((ushort)_names[index].Length);
            writer.WriteBytes(_names[index]);
        }
    }

    private static string Cut(string name)
    {
        return name.Length <= NameMaximum ? name : name[..NameMaximum];
    }
}
