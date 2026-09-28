using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to attack a mobile. (0x05, 5 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x05, PacketSizing.Fixed, Length = 5, Description = "Attack request")]
public sealed class AttackRequestPacket : BaseFixedPacket<AttackRequestPacket>, IIncomingPacket<AttackRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out AttackRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new AttackRequestPacket() : null;

        return packet is not null;
    }
}
