using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     What a player chose to sell (0x9F): the vendor, a count, and six bytes for each item chosen. A count that does not
///     match the size is refused.
/// </summary>
[PacketHandler(0x9F, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Vendor: sell reply")]
public sealed class VendorSellReplyPacket : BasePacket<VendorSellReplyPacket>, IIncomingPacket<VendorSellReplyPacket>
{
    private const int HeaderLength = 9;
    private const int LineLength = 6;

    public override int Length { get; }

    public Serial Vendor { get; }

    public IReadOnlyList<VendorSellReplyLine> Lines { get; }

    private VendorSellReplyPacket(int length, Serial vendor, IReadOnlyList<VendorSellReplyLine> lines)
    {
        Length = length;
        Vendor = vendor;
        Lines = lines;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out VendorSellReplyPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadSerial(out var vendor) || !reader.TryReadUInt16BigEndian(out var count) ||
            data.Length - HeaderLength != count * LineLength)
        {
            return false;
        }

        var lines = new List<VendorSellReplyLine>(count);

        for (var index = 0; index < count; index++)
        {
            if (!reader.TryReadSerial(out var item) || !reader.TryReadUInt16BigEndian(out var amount))
            {
                return false;
            }

            lines.Add(new(item, amount));
        }

        packet = new(data.Length, vendor, lines);

        return true;
    }
}
