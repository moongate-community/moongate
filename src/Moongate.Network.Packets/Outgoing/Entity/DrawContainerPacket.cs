using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x24, PacketSizing.Fixed, Length = 9, Description = "Draw Container")]

/// <summary>
/// Represents DrawContainerPacket.
/// </summary>
public class DrawContainerPacket : BaseGameNetworkPacket
{
    public UOItemEntity? Container { get; set; }

    public DrawContainerPacket()
        : base(0x24, 9) { }

    public DrawContainerPacket(UOItemEntity container)
        : this()
    {
        Container = container;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Container is null)
        {
            throw new InvalidOperationException("Container must be set before writing DrawContainerPacket.");
        }

        writer.Write(OpCode);
        writer.Write(Container.Id.Value);
        writer.Write((ushort)ResolveGumpId(Container));
        writer.Write((short)0x7D);
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => reader.Remaining == 8;

    private static int ResolveGumpId(UOItemEntity container)
    {
        if (container.GumpId is { } explicitGumpId)
        {
            return explicitGumpId;
        }

        if (ContainerLayoutSystem.ContainerBagDefsByItemId.TryGetValue(container.ItemId, out var definition) &&
            definition.GumpId is { } fallbackGumpId &&
            fallbackGumpId != 0)
        {
            return fallbackGumpId;
        }

        return 0;
    }
}
