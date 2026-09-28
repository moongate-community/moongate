using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to pick up an item. (0x07, 7 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x07, PacketSizing.Fixed, Length = 7, Description = "Lift request")]
public sealed class LiftRequestPacket : BaseFixedPacket<LiftRequestPacket>, IIncomingPacket<LiftRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out LiftRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new LiftRequestPacket() : null;

        return packet is not null;
    }
}
