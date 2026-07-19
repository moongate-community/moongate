using Moongate.Core.Types;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Move rejected (0x21): echoes the rejected sequence with the mover's true position, forcing a client snap-back.</summary>
[PacketDocumentation(PacketFamilyType.Movement, Length = 8, Name = "Char Move Rejection")]
public readonly record struct
    MoveRejectPacket(byte Sequence, ushort X, ushort Y, DirectionType Direction, sbyte Z) : IOutgoingPacket
{
    public const byte PacketId = 0x21;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Sequence);
        writer.Write(X);
        writer.Write(Y);
        writer.Write((byte)Direction);
        writer.Write(Z);
    }
}
