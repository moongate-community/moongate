using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A request to wear the held item. (0x13, 10 bytes). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0x13, PacketSizing.Fixed, Length = 10, Description = "Equip request")]
public sealed class EquipRequestPacket : BaseFixedPacket<EquipRequestPacket>, IIncomingPacket<EquipRequestPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out EquipRequestPacket? packet)
    {
        packet = HasValidHeader(data) ? new EquipRequestPacket() : null;

        return packet is not null;
    }
}
