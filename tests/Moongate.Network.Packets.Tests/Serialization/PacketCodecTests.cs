using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Tests.Support;

namespace Moongate.Network.Packets.Tests.Serialization;

public class PacketCodecTests
{
    [Fact]
    public void Encode_NullPacket_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PacketCodec.Encode(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Encode_InvalidDeclaredLength_ThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PacketCodec.Encode(new InvalidLengthPacket(length)));
    }

    [Fact]
    public void Encode_WriterCountDiffersFromDeclaredLength_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => PacketCodec.Encode(new MismatchedLengthPacket()));
    }
}
