using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to switch between peace and war. (0x72, 5 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x72, PacketSizing.Fixed, Length = 5, Description = "War mode request")]
public sealed class WarModeRequestPacket : BaseFixedPacket<WarModeRequestPacket>, IIncomingPacket<WarModeRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out WarModeRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new WarModeRequestPacket() : null;

        return packet is not null;
    }
}
