using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows an item worn by a mobile (0x2E), as ModernUO's equip update: the item, its graphic, its layer, the wearer
///     and its hue.
/// </summary>
[PacketHandler(0x2E, PacketSizing.Fixed, Length = 15)]
public sealed class WornItemPacket : BaseFixedPacket<WornItemPacket>, IOutgoingPacket
{
    public Serial Item { get; }

    public int ItemId { get; }

    public LayerType Layer { get; }

    public Serial Wearer { get; }

    public ushort Hue { get; }

    public WornItemPacket(ItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.MobileId is not { } wearer || item.Layer is not { } layer)
        {
            throw new ArgumentException($"{item} is not worn.", nameof(item));
        }

        Item = item.Id;
        ItemId = item.ItemId;
        Layer = layer;
        Wearer = wearer;
        Hue = item.Hue.Value;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Item);
        writer.WriteUInt16BigEndian((ushort)ItemId);
        writer.WriteUInt16BigEndian((ushort)Layer);
        writer.WriteSerial(Wearer);
        writer.WriteUInt16BigEndian(Hue);
    }
}
