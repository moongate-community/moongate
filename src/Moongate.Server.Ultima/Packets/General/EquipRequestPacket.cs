using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The player drops the held item on a paperdoll to wear it (0x13, 10 bytes): the item, the layer the client
///     suggests and the mobile whose paperdoll it is.
/// </summary>
[PacketHandler(0x13, PacketSizing.Fixed, Length = 10, Description = "Equip request")]
public sealed class EquipRequestPacket : BaseFixedPacket<EquipRequestPacket>, IIncomingPacket<EquipRequestPacket>
{
    public required Serial Item { get; init; }

    public required LayerType Layer { get; init; }

    public required Serial Mobile { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out EquipRequestPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var item) ||
            !reader.TryReadByte(out var layer) ||
            !reader.TryReadUInt32BigEndian(out var mobile))
        {
            return false;
        }

        packet = new() { Item = new(item), Layer = (LayerType)layer, Mobile = new(mobile) };

        return true;
    }
}
