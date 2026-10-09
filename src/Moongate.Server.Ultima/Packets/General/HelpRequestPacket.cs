using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The Help button of the paperdoll. (0x9B, 258 bytes). Only its frame is read: the server has no help pages yet.
/// </summary>
[PacketHandler(0x9B, PacketSizing.Fixed, Length = 258, Description = "Help request")]
public sealed class HelpRequestPacket : BaseFixedPacket<HelpRequestPacket>, IIncomingPacket<HelpRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out HelpRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new HelpRequestPacket() : null;

        return packet is not null;
    }
}
