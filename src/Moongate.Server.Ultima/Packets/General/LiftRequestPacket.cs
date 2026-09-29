using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The player picks up an item (0x07, 7 bytes): its serial and how many of its stack.
/// </summary>
[PacketHandler(0x07, PacketSizing.Fixed, Length = 7, Description = "Lift request")]
public sealed class LiftRequestPacket : BaseFixedPacket<LiftRequestPacket>, IIncomingPacket<LiftRequestPacket>
{
    public required Serial Item { get; init; }

    public required int Amount { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out LiftRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var item) || !reader.TryReadUInt16BigEndian(out var amount))
        {
            return false;
        }

        packet = new() { Item = new(item), Amount = amount };

        return true;
    }
}
