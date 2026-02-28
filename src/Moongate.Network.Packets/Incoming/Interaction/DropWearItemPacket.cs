using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Incoming.Interaction;

[PacketHandler(0x13, PacketSizing.Fixed, Length = 10, Description = "Drop->Wear Item")]

/// <summary>
/// Represents DropWearItemPacket.
/// </summary>
public class DropWearItemPacket : BaseGameNetworkPacket
{
    /// <summary>
    /// Gets or sets dropped item serial.
    /// </summary>
    public Serial ItemSerial { get; set; }

    /// <summary>
    /// Gets or sets requested equip layer. Do not trust this value blindly.
    /// </summary>
    public ItemLayerType Layer { get; set; }

    /// <summary>
    /// Gets or sets target mobile serial.
    /// </summary>
    public Serial PlayerSerial { get; set; }

    public DropWearItemPacket()
        : base(0x13, 10) { }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((uint)ItemSerial);
        writer.Write((byte)Layer);
        writer.Write((uint)PlayerSerial);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 9)
        {
            return false;
        }

        ItemSerial = (Serial)reader.ReadUInt32();
        Layer = (ItemLayerType)reader.ReadByte();
        PlayerSerial = (Serial)reader.ReadUInt32();

        return reader.Remaining == 0;
    }
}
