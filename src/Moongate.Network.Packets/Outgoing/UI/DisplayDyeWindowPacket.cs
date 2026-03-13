using Moongate.Network.Packets.Base;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.UI;

/// <summary>
/// Outbound dye window packet (opcode 0x95).
/// This packet intentionally has no PacketHandler attribute to avoid registry opcode collision
/// with inbound DyeWindowPacket, since registry is currently direction-agnostic.
/// </summary>
public sealed class DisplayDyeWindowPacket : BaseGameNetworkPacket
{
    public const ushort DefaultModel = 0x0FAB;

    public Serial TargetSerial { get; set; }

    public ushort Model { get; set; } = DefaultModel;

    public DisplayDyeWindowPacket()
        : base(0x95, 9) { }

    public DisplayDyeWindowPacket(Serial targetSerial, ushort model = DefaultModel)
        : this()
    {
        TargetSerial = targetSerial;
        Model = model;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((uint)TargetSerial);
        writer.Write((ushort)0);
        writer.Write(Model);
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => reader.Remaining == 8;
}
