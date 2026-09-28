using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to drop the held item (the grid byte of clients from 6.0.1.7). (0x08, 15 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x08, PacketSizing.Fixed, Length = 15, Description = "Drop request")]
public sealed class DropRequestPacket : BaseFixedPacket<DropRequestPacket>, IIncomingPacket<DropRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out DropRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new DropRequestPacket() : null;

        return packet is not null;
    }
}
