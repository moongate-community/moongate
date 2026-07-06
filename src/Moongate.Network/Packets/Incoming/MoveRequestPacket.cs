using Moongate.Core.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Move request (0x02): one step or turn, with anti-fastwalk key.</summary>
public readonly record struct MoveRequestPacket(DirectionType Direction, byte Sequence, uint FastwalkKey)
{
    public const byte PacketId = 0x02;

    public static MoveRequestPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        var direction = (DirectionType)reader.ReadByte();
        var sequence = reader.ReadByte();
        var fastwalkKey = reader.ReadUInt32();

        return new MoveRequestPacket(direction, sequence, fastwalkKey);
    }
}
