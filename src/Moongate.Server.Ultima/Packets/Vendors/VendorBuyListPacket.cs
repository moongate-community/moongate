using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     The prices and names of the lines of a shop window (0x74), in the order of the items of the container that
///     0x3C sent, which are written from the last to the first.
/// </summary>
[PacketHandler(0x74, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Vendor: buy list")]
public sealed class VendorBuyListPacket : BasePacket<VendorBuyListPacket>, IOutgoingPacket
{
    private const int HeaderLength = 8;
    private const int LineOverhead = 6;
    private const int NameMaximum = 253;

    private readonly VendorBuyListEntry[] _lines;

    public override int Length { get; }

    public Serial ShopContainer { get; }

    public VendorBuyListPacket(Serial shopContainer, IEnumerable<VendorBuyListEntry> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        ShopContainer = shopContainer;
        _lines = lines
            .Select(line => line with { Name = Cut(line.Name) })
            .ToArray();
        Length = HeaderLength + _lines.Sum(line => LineOverhead + line.Name.Length);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(ShopContainer);
        writer.WriteByte((byte)_lines.Length);

        foreach (var line in _lines)
        {
            writer.WriteUInt32BigEndian((uint)line.Price);
            writer.WriteByte((byte)(line.Name.Length + 1));
            writer.WriteNullTerminatedAscii(line.Name);
        }
    }

    private static string Cut(string name)
    {
        return name.Length <= NameMaximum ? name : name[..NameMaximum];
    }
}
