using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request for a mobile's status or skills. (0x34, 10 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x34, PacketSizing.Fixed, Length = 10, Description = "Mobile query")]
public sealed class MobileQueryPacket : BaseFixedPacket<MobileQueryPacket>, IIncomingPacket<MobileQueryPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MobileQueryPacket? packet)
    {
        packet = HasValidHeader(data) ? new MobileQueryPacket() : null;

        return packet is not null;
    }
}
