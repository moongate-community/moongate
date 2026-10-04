using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Plays an animation of a mobile (0x6E, 14 bytes), as ModernUO's Animate: the action of its body, how many frames
///     and how many times, backwards or not, repeated or not, and the delay between frames.
/// </summary>
[PacketHandler(0x6E, PacketSizing.Fixed, Length = 14)]
public sealed class MobileAnimationPacket : BaseFixedPacket<MobileAnimationPacket>, IOutgoingPacket
{
    public Serial Serial { get; }

    public int Action { get; }

    public int FrameCount { get; }

    public int RepeatCount { get; }

    public bool Forward { get; }

    public bool Repeat { get; }

    public int Delay { get; }

    public MobileAnimationPacket(
        Serial serial,
        int action,
        int frameCount,
        int repeatCount,
        bool forward = true,
        bool repeat = false,
        int delay = 0
    )
    {
        Serial = serial;
        Action = action;
        FrameCount = frameCount;
        RepeatCount = repeatCount;
        Forward = forward;
        Repeat = repeat;
        Delay = delay;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)Action);
        writer.WriteUInt16BigEndian((ushort)FrameCount);
        writer.WriteUInt16BigEndian((ushort)RepeatCount);
        // The protocol's field is "reverse".
        writer.WriteByte(Forward ? (byte)0 : (byte)1);
        writer.WriteByte(Repeat ? (byte)1 : (byte)0);
        writer.WriteByte((byte)Delay);
    }
}
