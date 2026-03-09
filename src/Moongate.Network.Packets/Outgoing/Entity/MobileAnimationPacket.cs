using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x6E, PacketSizing.Fixed, Length = 14, Description = "Mobile Animation")]
public sealed class MobileAnimationPacket : BaseGameNetworkPacket
{
    public Serial MobileId { get; set; } = Serial.Zero;

    public short Action { get; set; }

    public short FrameCount { get; set; } = 5;

    public short RepeatCount { get; set; } = 1;

    public bool Forward { get; set; } = true;

    public bool Repeat { get; set; }

    public byte Delay { get; set; }

    public MobileAnimationPacket()
        : base(0x6E, 14) { }

    public MobileAnimationPacket(
        Serial mobileId,
        short action,
        short frameCount = 5,
        short repeatCount = 1,
        bool forward = true,
        bool repeat = false,
        byte delay = 0
    ) : this()
    {
        MobileId = mobileId;
        Action = action;
        FrameCount = frameCount;
        RepeatCount = repeatCount;
        Forward = forward;
        Repeat = repeat;
        Delay = delay;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(MobileId.Value);
        writer.Write(Action);
        writer.Write(FrameCount);
        writer.Write(RepeatCount);
        writer.Write(!Forward); // protocol uses reverse flag.
        writer.Write(Repeat);
        writer.Write(Delay);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 13)
        {
            return false;
        }

        MobileId = (Serial)reader.ReadUInt32();
        Action = reader.ReadInt16();
        FrameCount = reader.ReadInt16();
        RepeatCount = reader.ReadInt16();
        var reverse = reader.ReadBoolean();
        Repeat = reader.ReadBoolean();
        Delay = reader.ReadByte();
        Forward = !reverse;

        return reader.Remaining == 0;
    }
}
