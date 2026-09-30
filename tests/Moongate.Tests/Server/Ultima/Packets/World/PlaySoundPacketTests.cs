using Moongate.Core.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class PlaySoundPacketTests
{
    [Fact]
    public void Encode_WritesTheSoundOnceAtTheLocation()
    {
        // As ModernUO's CreateSoundEffect: 0x54, flags 1, sound, volume 0, x, y, z.
        Assert.Equal(
            Convert.FromHexString("5401006900000640065C0005"),
            PacketCodec.Encode(new PlaySoundPacket(0x69, new Point3D(1600, 1628, 5)))
        );
    }
}
