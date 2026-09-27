using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Types.Network;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Server.Services.Network.Middleware;

public sealed class UoEncryptionMiddlewareTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task ProcessAsync_EncryptedPolHandshakes_SurviveEverySplitAndCoalescedFollowingPacket(bool game)
    {
        foreach (var vector in PolEncryptionFixture.Read().Where(x => x.GetProperty("Version").GetString() != "none"))
        {
            var version = vector.GetProperty("Version").GetString()!;
            var seed = vector.GetProperty("Seed").GetUInt32();
            var prefix = PolEncryptionFixture.Seed(seed, !game);
            var encrypted = Convert.FromHexString(vector.GetProperty(game ? "GameLoginAndPing" : "LoginPacketAndSelect").GetString()!);
            byte[] wire = [.. prefix, .. encrypted];
            byte[] expected = [.. prefix, .. PolEncryptionFixture.PlainLogin(seed, game), .. (game ? new byte[] { 0x73, 0x42 } : new byte[] { 0xA0, 0, 0 })];
            for (var split = 1; split < wire.Length; split++)
            {
                var middleware = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, UoEncryptionProfile.Parse(version), game);
                var first = (await middleware.ProcessAsync(null, wire.AsMemory(0, split))).ToArray();
                var second = (await middleware.ProcessAsync(null, wire.AsMemory(split))).ToArray();
                Assert.Equal(expected, first.Concat(second));
            }
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ProcessAsync_OptionalPlaintextPassesThrough_RequiredRejects(bool game)
    {
        const uint seed = 0x12345678;
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, !game), .. PolEncryptionFixture.PlainLogin(seed, game)];
        var optional = new UoEncryptionMiddleware(NetworkEncryptionMode.Optional, UoEncryptionProfile.Parse("67.0.117.0"), game);
        Assert.Equal(wire, (await optional.ProcessAsync(null, wire)).ToArray());
        byte[] ping = [0x73, 0x42];
        Assert.Equal(ping, (await optional.ProcessAsync(null, ping)).ToArray());
        Assert.Equal(ping, (await optional.ProcessSendAsync(null, ping)).ToArray());
        var required = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, UoEncryptionProfile.Parse("67.0.117.0"), game);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await required.ProcessAsync(null, wire));
    }

    [Theory, InlineData(false, false), InlineData(true, false), InlineData(false, true), InlineData(true, true)]
    public async Task ProcessAsync_ValidFullWidthOrPaddedCredentials_PreserveCanonicalPacketBehavior(bool game, bool fullWidth)
    {
        const uint seed = 0x12345678;
        var profile = UoEncryptionProfile.Parse("67.0.117.0");
        var plaintext = PolEncryptionFixture.PlainLogin(seed, game);
        var credentials = plaintext.AsSpan(game ? 5 : 1, 60);
        if (fullWidth)
        {
            credentials.Fill((byte)'a');
        }
        else
        {
            // Padding after an earlier NUL is ignored by both canonical parsers.
            credentials[29] = 0xFE;
            credentials[59] = 0xFE;
        }
        Assert.True(game ? GameLoginPacket.TryParse(plaintext, out _) : AccountLoginPacket.TryParse(plaintext, out _));
        foreach (var mode in new[] { NetworkEncryptionMode.Optional, NetworkEncryptionMode.Required })
        {
            foreach (var encrypted in new[] { false, true })
            {
                byte[] prefix = PolEncryptionFixture.Seed(seed, !game);
                byte[] wire = [.. prefix, .. (encrypted ? PolEncryptionFixture.EncryptModernLogin(plaintext, game) : plaintext)];
                var middleware = new UoEncryptionMiddleware(mode, profile, game);
                if (!encrypted && mode == NetworkEncryptionMode.Required)
                {
                    await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, wire));
                }
                else
                {
                    Assert.Equal(prefix.Concat(plaintext), (await middleware.ProcessAsync(null, wire)).ToArray());
                }
            }
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ProcessAsync_MalformedCredentialsWithValidOpcodeAndTerminators_FailClosed(bool game)
    {
        const uint seed = 0x12345678;
        var plaintext = PolEncryptionFixture.PlainLogin(seed, game);
        plaintext[game ? 5 : 1] = 0xFF;
        Assert.False(game ? GameLoginPacket.TryParse(plaintext, out _) : AccountLoginPacket.TryParse(plaintext, out _));
        foreach (var mode in new[] { NetworkEncryptionMode.Optional, NetworkEncryptionMode.Required })
        {
            byte[] wire = [.. PolEncryptionFixture.Seed(seed, !game), .. PolEncryptionFixture.EncryptModernLogin(plaintext, game)];
            var middleware = new UoEncryptionMiddleware(mode, UoEncryptionProfile.Parse("67.0.117.0"), game);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, wire));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, wire));
        }
    }

    [Theory, InlineData(NetworkEncryptionMode.Optional), InlineData(NetworkEncryptionMode.Required)]
    public async Task ProcessAsync_WrongProfilePreservingOpcodeAndTerminators_FailsBeforePublishing(NetworkEncryptionMode mode)
    {
        const uint seed = 0x12345678;
        var plaintext = PolEncryptionFixture.PlainLogin(seed, false);
        plaintext.AsSpan(1, 30).Clear();
        plaintext.AsSpan(31, 30).Clear();
        plaintext.AsSpan(1, 20).Fill((byte)'a');
        plaintext.AsSpan(31, 20).Fill((byte)'b');
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, true), .. PolEncryptionFixture.EncryptModernLogin(plaintext, false, "7.0.117.0")];
        var middleware = new UoEncryptionMiddleware(mode, UoEncryptionProfile.Parse("67.0.117.0"), false);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, wire));
    }

    [Fact]
    public async Task ProcessAsync_RawLoginSeedBecomesVersionedSeedForExistingLoginHandler()
    {
        var vector = PolEncryptionFixture.Read().First(x => x.GetProperty("Version").GetString() == "67.0.117.0");
        var seed = vector.GetProperty("Seed").GetUInt32();
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, false), .. Convert.FromHexString(vector.GetProperty("LoginPacket").GetString()!)];
        var middleware = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, UoEncryptionProfile.Parse("67.0.117.0"), false);
        var output = new List<byte>();
        foreach (var b in wire)
        {
            output.AddRange((await middleware.ProcessAsync(null, new byte[] { b })).ToArray());
        }
        Assert.Equal(PolEncryptionFixture.Seed(seed, true).Concat(PolEncryptionFixture.PlainLogin(seed, false)), output);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ProcessAsync_WrongKeysOrMalformedPacketFailClosed(bool game)
    {
        var vector = PolEncryptionFixture.Read().First(x => x.GetProperty("Version").GetString() == "67.0.117.0");
        var seed = vector.GetProperty("Seed").GetUInt32();
        var encrypted = Convert.FromHexString(vector.GetProperty(game ? "GameLogin" : "LoginPacket").GetString()!);
        encrypted[0] ^= 0x40;
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, !game), .. encrypted];
        var middleware = new UoEncryptionMiddleware(NetworkEncryptionMode.Optional, UoEncryptionProfile.Parse("7.0.117.0"), game);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, wire));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, new byte[] { 0x73, 0 }));
    }

    [Theory, InlineData(0u), InlineData(uint.MaxValue)]
    public async Task ProcessAsync_InvalidOrKrSeedFailsWithoutRetainingUnboundedInput(uint seed)
    {
        var middleware = new UoEncryptionMiddleware(NetworkEncryptionMode.Optional, UoEncryptionProfile.Parse("67.0.117.0"), true);
        byte[] input = [.. PolEncryptionFixture.Seed(seed, false), .. new byte[100000]];
        await Assert.ThrowsAsync<InvalidDataException>(async () => await middleware.ProcessAsync(null, input));
    }

    [Fact]
    public async Task ProcessSendAsync_GameUsesIndependentPerConnectionStream_LoginRepliesStayPlaintext()
    {
        var vector = PolEncryptionFixture.Read().First(x => x.GetProperty("Version").GetString() == "67.0.117.0");
        var profile = UoEncryptionProfile.Parse("67.0.117.0");
        var seed = vector.GetProperty("Seed").GetUInt32();
        var first = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, profile, true);
        var second = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, profile, true);
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, false), .. Convert.FromHexString(vector.GetProperty("GameLogin").GetString()!)];
        await Task.WhenAll(first.ProcessAsync(null, wire).AsTask(), second.ProcessAsync(null, wire).AsTask());
        byte[] payload = [0x4C, 0xD0];
        var prefix = Convert.FromHexString(vector.GetProperty("SendPrefix").GetString()!);
        byte[] expected = [(byte)(payload[0] ^ prefix[0]), (byte)(payload[1] ^ prefix[1] ^ 1)];
        Assert.Empty((await first.ProcessSendAsync(null, ReadOnlyMemory<byte>.Empty)).ToArray());
        Assert.Equal(expected, (await first.ProcessSendAsync(null, payload)).ToArray());
        await first.ProcessAsync(null, new byte[257]);
        Assert.Equal(expected, (await second.ProcessSendAsync(null, payload)).ToArray());
        var login = new UoEncryptionMiddleware(NetworkEncryptionMode.Required, profile, false);
        byte[] loginWire = [.. PolEncryptionFixture.Seed(seed, true), .. Convert.FromHexString(vector.GetProperty("LoginPacket").GetString()!)];
        await login.ProcessAsync(null, loginWire);
        Assert.Equal(payload, (await login.ProcessSendAsync(null, payload)).ToArray());
    }

    [Fact]
    public async Task ProcessAsync_DisabledLeavesExistingProtocolUntouched()
    {
        var middleware = new UoEncryptionMiddleware(NetworkEncryptionMode.Disabled, UoEncryptionProfile.Parse("none"), false);
        byte[] input = [0xEF, 1, 2];
        Assert.Equal(input, (await middleware.ProcessAsync(null, input)).ToArray());
    }
}
