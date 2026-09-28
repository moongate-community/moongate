using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A double click on an object. (0x06, 5 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x06, PacketSizing.Fixed, Length = 5, Description = "Use request")]
public sealed class UseRequestPacket : BaseFixedPacket<UseRequestPacket>, IIncomingPacket<UseRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out UseRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new UseRequestPacket() : null;

        return packet is not null;
    }
}
