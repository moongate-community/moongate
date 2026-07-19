using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Container content (0x3C): every item inside a container, in one variable-length packet. Sent right
/// after draw container (0x24) to fill the gump that was just opened. Each entry repeats the container's
/// serial, which is how the client knows where to draw it.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, IsVariableLength = true)]
public readonly record struct ContainerContentPacket(Serial Container, IReadOnlyList<ContainerItem> Items)
    : IOutgoingPacket
{
    public const byte PacketId = 0x3C;

    private const int HeaderLength = 5; // id + length + count
    private const int EntryLength = 20; // modern client: 19 plus the grid-location byte

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + EntryLength * Items.Count));
        writer.Write((ushort)Items.Count);

        foreach (var item in Items)
        {
            writer.Write(item.Serial);
            writer.Write(item.ItemId);
            writer.Write((byte)0); // itemId offset, always zero for us
            writer.Write(item.Amount);
            writer.Write((short)item.Position.X);
            writer.Write((short)item.Position.Y);
            writer.Write((byte)0); // grid location, unused: the client packs the gump itself
            writer.Write(Container);
            writer.Write(item.Hue);
        }
    }
}
