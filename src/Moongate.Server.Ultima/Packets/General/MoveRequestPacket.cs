using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A step the client asks to take. (0x02, 7 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x02, PacketSizing.Fixed, Length = 7, Description = "Move request")]
public sealed class MoveRequestPacket : BaseFixedPacket<MoveRequestPacket>, IIncomingPacket<MoveRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MoveRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new MoveRequestPacket() : null;

        return packet is not null;
    }
}
