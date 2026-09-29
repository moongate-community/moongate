using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The items inside a container (0x3C), each at its position in the gump; clients from 6.0.1.7 also read a grid
///     byte. The items are copied when the packet is built, so later changes to them are not sent.
/// </summary>
[PacketHandler(0x3C, PacketSizing.Variable, MinimumLength = HeaderLength)]
public sealed class ContainerContentPacket : BasePacket<ContainerContentPacket>, IOutgoingPacket
{
    private const int HeaderLength = 5;
    private const int ItemLength = 19;
    private const int GridItemLength = 20;

    public override int Length { get; }

    public IReadOnlyList<ContainerItemEntry> Items { get; }

    public bool GridBytes { get; }

    public ContainerContentPacket(IEnumerable<ItemEntity> items, bool gridBytes)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items.Select(
                         item => new ContainerItemEntry(
                             item.Id,
                             item.ItemId,
                             item.Amount,
                             item.GridX ?? 0,
                             item.GridY ?? 0,
                             item.ContainerId ?? default,
                             item.Hue
                         )
                     )
                     .ToArray();
        GridBytes = gridBytes;
        Length = HeaderLength + Items.Count * (gridBytes ? GridItemLength : ItemLength);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian((ushort)Items.Count);

        foreach (var item in Items)
        {
            writer.WriteSerial(item.Serial);
            writer.WriteUInt16BigEndian((ushort)item.ItemId);
            writer.WriteByte(0); // Graphic offset.
            writer.WriteUInt16BigEndian((ushort)Math.Min(item.Amount, ushort.MaxValue));
            writer.WriteUInt16BigEndian(unchecked((ushort)(short)item.GridX));
            writer.WriteUInt16BigEndian(unchecked((ushort)(short)item.GridY));

            if (GridBytes)
            {
                writer.WriteByte(0); // Grid index: the client places the item itself.
            }

            writer.WriteSerial(item.Container);
            writer.WriteUInt16BigEndian(item.Hue.Value);
        }
    }
}
