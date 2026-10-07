using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Death;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     What a corpse is drawn wearing (0x89): each item with its layer, written as the layer plus one, and a zero byte
///     to end, as ModernUO does. The client needs the items first, in a container content (0x3C) of the corpse.
/// </summary>
[PacketHandler(0x89, PacketSizing.Variable, MinimumLength = HeaderLength)]
public sealed class CorpseEquipmentPacket : BasePacket<CorpseEquipmentPacket>, IOutgoingPacket
{
    private const int HeaderLength = 8;
    private const int EntryLength = 5;

    public override int Length { get; }

    public Serial Corpse { get; }

    public IReadOnlyList<CorpseWornItem> Items { get; }

    public CorpseEquipmentPacket(Serial corpse, IReadOnlyList<CorpseWornItem> items)
    {
        Corpse = corpse;
        Items = items;
        Length = HeaderLength + items.Count * EntryLength;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Corpse);

        foreach (var item in Items)
        {
            writer.WriteByte((byte)((int)item.Layer + 1));
            writer.WriteSerial(item.Serial);
        }

        writer.WriteByte(0);
    }
}
