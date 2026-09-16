using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Serialization;

namespace Moongate.Network.Packets.Tests.Incoming.Login;

public class AccountLoginPacketTests
{
    private static readonly byte[] Fixture = CreateFixture(0xFF);

    [Fact]
    public void TryDecode_KnownFixture_ReadsCredentialsAndRawKey()
    {
        Assert.True(PacketCodec.TryDecode<AccountLoginPacket>(Fixture, out var packet));
        Assert.Equal(0x80, packet.OpCode);
        Assert.Equal(62, packet.Length);
        Assert.Equal("a", packet.Account);
        Assert.Equal("b", packet.Password);
        Assert.Equal((byte)0xFF, packet.NextLoginKey);

        foreach (var rawKey in new byte[] { 0x00, 0x5D })
        {
            Assert.True(PacketCodec.TryDecode<AccountLoginPacket>(CreateFixture(rawKey), out var rawPacket));
            Assert.Equal(rawKey, rawPacket.NextLoginKey);
        }
    }

    [Fact]
    public void TryDecode_FullWidthCredentials_PreservesAllCharacters()
    {
        var fixture = new byte[62];
        fixture[0] = 0x80;
        "123456789012345678901234567890"u8.CopyTo(fixture.AsSpan(1));
        "abcdefghijklmnopqrstuvwxyz1234"u8.CopyTo(fixture.AsSpan(31));
        fixture[61] = 0x5D;

        Assert.True(PacketCodec.TryDecode<AccountLoginPacket>(fixture, out var packet));
        Assert.Equal("123456789012345678901234567890", packet.Account);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz1234", packet.Password);
    }

    [Fact]
    public void TryDecode_IncompleteWrongAppendedOrNonAsciiFrame_ReturnsFalse()
    {
        for (var length = 0; length < Fixture.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<AccountLoginPacket>(Fixture.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture.ToArray();
        wrongOpcode[0] = 0x81;
        var nonAscii = Fixture.ToArray();
        nonAscii[1] = 0x80;
        Assert.False(PacketCodec.TryDecode<AccountLoginPacket>(wrongOpcode, out _));
        Assert.False(PacketCodec.TryDecode<AccountLoginPacket>(nonAscii, out _));
        Assert.False(PacketCodec.TryDecode<AccountLoginPacket>([.. Fixture, 0x00], out _));
    }

    [Theory, InlineData("1234567890123456789012345678901", "b"), InlineData("é", "b"), InlineData("a", "b\0")]
    public void Constructor_InvalidCredential_ThrowsArgumentException(string account, string password)
    {
        Assert.Throws<ArgumentException>(() => new AccountLoginPacket(account, password, 0xFF));
    }

    private static byte[] CreateFixture(byte nextLoginKey)
    {
        var fixture = new byte[62];
        fixture[0] = 0x80;
        fixture[1] = (byte)'a';
        fixture[31] = (byte)'b';
        fixture[61] = nextLoginKey;
        return fixture;
    }
}
