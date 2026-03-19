using System.Buffers.Binary;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x89, PacketSizing.Variable, Description = "Corpse Clothing")]
public sealed class CorpseClothingPacket : BaseGameNetworkPacket
{
    public UOItemEntity? Corpse { get; set; }

    public CorpseClothingPacket()
        : base(0x89) { }

    public CorpseClothingPacket(UOItemEntity corpse)
        : this()
    {
        Corpse = corpse;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Corpse is null)
        {
            throw new InvalidOperationException("Corpse must be set before writing CorpseClothingPacket.");
        }

        var start = writer.Position;

        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write(Corpse.Id.Value);

        foreach (var item in Corpse.Items)
        {
            if (!item.TryGetCustomInteger(CorpsePropertyKeys.EquippedLayer, out var rawLayer))
            {
                continue;
            }

            var layer = (ItemLayerType)rawLayer;

            if (layer == ItemLayerType.Invalid)
            {
                continue;
            }

            writer.Write((byte)((byte)layer + 1));
            writer.Write(item.Id.Value);
        }

        writer.Write((byte)ItemLayerType.Invalid);

        var length = (ushort)(writer.Position - start);
        BinaryPrimitives.WriteUInt16BigEndian(writer.RawBuffer[(start + 1)..], length);
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => reader.Remaining >= 7;
}
