using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The view range the client wants. (0xC8, 2 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xC8, PacketSizing.Fixed, Length = 2, Description = "Update range")]
public sealed class UpdateRangePacket : BaseFixedPacket<UpdateRangePacket>, IIncomingPacket<UpdateRangePacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out UpdateRangePacket? packet)
    {
        packet = HasValidHeader(data) ? new UpdateRangePacket() : null;

        return packet is not null;
    }
}
