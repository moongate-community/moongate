using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows one item inside a container (0x25): 21 bytes with the grid byte clients from 6.0.1.7 read, 20 before. The
///     item is copied when the packet is built, so later changes to it are not sent.
/// </summary>
/// <remarks>
///     Declared variable because its length depends on the client version; it has no length field on the wire.
/// </remarks>
[PacketHandler(0x25, PacketSizing.Variable, MinimumLength = ShortLength)]
public sealed class ContainerItemUpdatePacket : BasePacket<ContainerItemUpdatePacket>, IOutgoingPacket
{
    private const int ShortLength = 20;
    private const int GridLength = 21;

    public override int Length { get; }

    public ContainerItemEntry Item { get; }

    public bool GridBytes { get; }

    public ContainerItemUpdatePacket(ItemEntity item, bool gridBytes)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = new(item.Id, item.ItemId, item.Amount, item.GridX ?? 0, item.GridY ?? 0, item.ContainerId ?? default, item.Hue);
        GridBytes = gridBytes;
        Length = gridBytes ? GridLength : ShortLength;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Item.Serial);
        writer.WriteUInt16BigEndian((ushort)Item.ItemId);
        writer.WriteByte(0); // Graphic offset.
        writer.WriteUInt16BigEndian((ushort)Math.Min(Item.Amount, ushort.MaxValue));
        writer.WriteUInt16BigEndian(unchecked((ushort)(short)Item.GridX));
        writer.WriteUInt16BigEndian(unchecked((ushort)(short)Item.GridY));

        if (GridBytes)
        {
            writer.WriteByte(0); // Grid index: the client places the item itself.
        }

        writer.WriteSerial(Item.Container);
        writer.WriteUInt16BigEndian(Item.Hue.Value);
    }
}
