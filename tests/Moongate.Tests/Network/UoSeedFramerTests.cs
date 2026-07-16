using Moongate.Network.Framing;

namespace Moongate.Tests.Network;

public class UoSeedFramerTests
{
    private readonly UoSeedFramer _framer = new();

    [Fact]
    public void TryReadFrame_AfterLoginSeedPacket_KeepsFramingNormally()
    {
        var loginSeed = new byte[21];
        loginSeed[0] = 0xEF;

        Assert.True(_framer.TryReadFrame(loginSeed, out _));

        // A subsequent account-login (0x80, 62 bytes fixed) frames as a normal packet, not a seed.
        var accountLogin = new byte[62];
        accountLogin[0] = 0x80;

        Assert.True(_framer.TryReadFrame(accountLogin, out var length));
        Assert.Equal(62, length);
    }

    [Fact]
    public void TryReadFrame_LoginSeedPacketFirst_FramesAsNormalPacket()
    {
        // Login connection: new clients open with the 0xEF login-seed packet (21 bytes fixed).
        var loginSeed = new byte[21];
        loginSeed[0] = 0xEF;

        Assert.True(_framer.TryReadFrame(loginSeed, out var length));
        Assert.Equal(21, length);
    }

    [Fact]
    public void TryReadFrame_PartialRawSeed_NeedsMoreBytes()
        => Assert.False(_framer.TryReadFrame([0x12, 0x34, 0x56], out _));

    [Fact]
    public void TryReadFrame_RawSeedFirst_EmitsFourByteSeedFrame()
    {
        // Game-server reconnect: raw 4-byte seed with no packet id.
        var seed = new byte[] { 0x12, 0x34, 0x56, 0x78 };

        Assert.True(_framer.TryReadFrame(seed, out var length));
        Assert.Equal(4, length);
    }

    [Fact]
    public void TryReadFrame_SeedThenGameLogin_EmitsSeedThenPacket()
    {
        var seed = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var gameLogin = new byte[65];
        gameLogin[0] = 0x91;
        var buffer = new byte[seed.Length + gameLogin.Length];
        seed.CopyTo(buffer, 0);
        gameLogin.CopyTo(buffer, seed.Length);

        Assert.True(_framer.TryReadFrame(buffer, out var seedLength));
        Assert.Equal(4, seedLength);

        // After the seed is consumed, the next frame is the 0x91 game login (65 bytes).
        Assert.True(_framer.TryReadFrame(buffer.AsSpan(seedLength), out var packetLength));
        Assert.Equal(65, packetLength);
    }
}
