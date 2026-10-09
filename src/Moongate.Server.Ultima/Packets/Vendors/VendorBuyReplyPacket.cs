using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     What a player chose to buy (0x3B): the vendor, a flag, and a line of seven bytes for each item chosen. The flag is
///     2 for a purchase; anything else is a cancel.
/// </summary>
[PacketHandler(0x3B, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Vendor: buy reply")]
public sealed class VendorBuyReplyPacket : BasePacket<VendorBuyReplyPacket>, IIncomingPacket<VendorBuyReplyPacket>
{
    /// <summary>
    ///     The flag of a purchase.
    /// </summary>
    public const byte BuyFlag = 2;

    private const int HeaderLength = 8;
    private const int LineLength = 7;

    public override int Length { get; }

    public Serial Vendor { get; }

    public byte Flag { get; }

    /// <summary>
    ///     The lines chosen; none on a cancel.
    /// </summary>
    public IReadOnlyList<VendorBuyReplyLine> Lines { get; }

    private VendorBuyReplyPacket(int length, Serial vendor, byte flag, IReadOnlyList<VendorBuyReplyLine> lines)
    {
        Length = length;
        Vendor = vendor;
        Flag = flag;
        Lines = lines;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out VendorBuyReplyPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data) || (data.Length - HeaderLength) % LineLength != 0)
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadSerial(out var vendor) || !reader.TryReadByte(out var flag))
        {
            return false;
        }

        var count = (data.Length - HeaderLength) / LineLength;
        var lines = new List<VendorBuyReplyLine>(count);

        for (var index = 0; index < count; index++)
        {
            if (!reader.TryReadByte(out var layer) ||
                !reader.TryReadSerial(out var item) ||
                !reader.TryReadUInt16BigEndian(out var amount))
            {
                return false;
            }

            lines.Add(new(layer, item, amount));
        }

        packet = new(data.Length, vendor, flag, lines);

        return true;
    }
}
