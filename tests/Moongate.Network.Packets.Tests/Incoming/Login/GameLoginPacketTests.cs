using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Serialization;

namespace Moongate.Network.Packets.Tests.Incoming.Login;

public class GameLoginPacketTests
{
    private static readonly byte[] Fixture = CreateFixture();

    [Fact]
    public void TryDecode_KnownFixture_ReadsKeyAndCredentials()
    {
        Assert.True(PacketCodec.TryDecode<GameLoginPacket>(Fixture, out var packet));
        Assert.Equal(0x91, packet.OpCode);
        Assert.Equal(65, packet.Length);
        Assert.Equal(0x12345678u, packet.AuthKey);
        Assert.Equal("a", packet.Account);
        Assert.Equal("b", packet.Password);
    }

    [Fact]
    public void TryDecode_IncompleteWrongAppendedOrNonAsciiFrame_ReturnsFalse()
    {
        for (var length = 0; length < Fixture.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<GameLoginPacket>(Fixture.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture.ToArray();
        wrongOpcode[0] = 0x90;
        var nonAscii = Fixture.ToArray();
        nonAscii[35] = 0xFF;
        Assert.False(PacketCodec.TryDecode<GameLoginPacket>(wrongOpcode, out _));
        Assert.False(PacketCodec.TryDecode<GameLoginPacket>(nonAscii, out _));
        Assert.False(PacketCodec.TryDecode<GameLoginPacket>([.. Fixture, 0x00], out _));
    }

    [Theory, InlineData("1234567890123456789012345678901", "b"), InlineData("a", "é")]
    public void Constructor_InvalidCredential_ThrowsArgumentException(string account, string password)
    {
        Assert.Throws<ArgumentException>(() => new GameLoginPacket(1, account, password));
    }

    private static byte[] CreateFixture()
    {
        var fixture = new byte[65];
        Convert.FromHexString("9112345678").CopyTo(fixture, 0);
        fixture[5] = (byte)'a';
        fixture[35] = (byte)'b';
        return fixture;
    }
}
