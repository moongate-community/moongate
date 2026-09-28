using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A single click on an object. (0x09, 5 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x09, PacketSizing.Fixed, Length = 5, Description = "Look request")]
public sealed class LookRequestPacket : BaseFixedPacket<LookRequestPacket>, IIncomingPacket<LookRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out LookRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new LookRequestPacket() : null;

        return packet is not null;
    }
}
