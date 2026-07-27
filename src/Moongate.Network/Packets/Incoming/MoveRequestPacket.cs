using Moongate.Core.Types;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Move request (0x02): one step or turn, with anti-fastwalk key.</summary>
[PacketDocumentation(PacketFamilyType.Movement, Length = 7)]
public readonly record struct MoveRequestPacket(DirectionType Direction, byte Sequence, uint FastwalkKey)
    : IIncomingPacket<MoveRequestPacket>
{
    public static byte PacketId => 0x02;

    public static MoveRequestPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var direction = (DirectionType)reader.ReadByte();
        var sequence = reader.ReadByte();
        var fastwalkKey = reader.ReadUInt32();

        return new(direction, sequence, fastwalkKey);
    }
}
