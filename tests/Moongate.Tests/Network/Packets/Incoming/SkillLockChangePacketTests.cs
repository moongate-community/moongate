using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class SkillLockChangePacketTests
{
    private static SkillLockChangePacket Read(ushort skillId, byte lockValue)
    {
        var buffer = new byte[6];
        buffer[0] = 0x3A;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), 6);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(3), skillId);
        buffer[5] = lockValue;

        var reader = new SpanReader(buffer);

        return SkillLockChangePacket.Read(ref reader);
    }

    [Fact]
    public void Read_ParsesSkillIdAndLock()
    {
        var packet = Read(40, (byte)SkillLockType.Down);

        Assert.Equal(40, packet.SkillId);
        Assert.Equal(SkillLockType.Down, packet.Lock);
    }

    [Fact]
    public void Read_LockAboveLocked_ClampsToUp()
        => Assert.Equal(SkillLockType.Up, Read(40, 9).Lock);

    [Fact]
    public void PacketId_Is0x3A()
        => Assert.Equal(0x3A, SkillLockChangePacket.PacketId);
}
