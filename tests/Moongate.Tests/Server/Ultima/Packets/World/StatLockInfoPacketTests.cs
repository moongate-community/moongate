using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class StatLockInfoPacketTests
{
    [Fact]
    public void Encode_WritesTheGeneralInfoSubcommand_WithTheSerialAndTheThreeLocksInBits()
    {
        var packet = new StatLockInfoPacket(new Serial(0x00000002), StatLockType.Down, StatLockType.Locked, StatLockType.Up);

        // strength 1 << 4, dexterity 2 << 2, intelligence 0
        Assert.Equal(
            new byte[] { 0xBF, 0x00, 0x0C, 0x00, 0x19, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x18 },
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_AllUp_IsZeroBits()
    {
        var packet = new StatLockInfoPacket(new Serial(1), StatLockType.Up, StatLockType.Up, StatLockType.Up);

        Assert.Equal(0x00, PacketCodec.Encode(packet)[11]);
    }
}
