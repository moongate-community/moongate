using System.Net.Sockets;
using System.Threading.Channels;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Network.Middleware;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Types.Network;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Integration.Network;

public sealed class UoEncryptionTransportTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task EncryptedTcpHandshake_DispatchesPlainFrames_AndUsesCorrectResponsePipeline(bool game)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        await using var sessionFixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(sessionFixture.Loop);
        var config = Config(NetworkEncryptionMode.Required);
        var options = game
            ? UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, [new UoCompressionMiddleware(sessions)])
            : UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default);
        var network = new NetworkService(options, registry.Service);
        var received = Channel.CreateUnbounded<(INetworkConnection Connection, byte[] Data)>();
        network.ConnectionAccepted += (_, e) => sessions.GetOrCreate(e.Connection);
        network.DataReceived += (_, e) => received.Writer.TryWrite((e.Connection, e.Data.ToArray()));
        var vector = PolEncryptionFixture.Read().First(x => x.GetProperty("Version").GetString() == "67.0.117.0");
        var seed = vector.GetProperty("Seed").GetUInt32();
        byte[] wire = [.. PolEncryptionFixture.Seed(seed, !game), .. Convert.FromHexString(vector.GetProperty(game ? "GameLoginAndPing" : "LoginPacketAndSelect").GetString()!)];
        try
        {
            await network.StartAsync();
            using var first = new TcpClient();
            using var second = new TcpClient();
            await first.ConnectAsync(network.Listeners[0].Endpoint, token);
            await second.ConnectAsync(network.Listeners[0].Endpoint, token);
            foreach (var peer in new[] { first, second })
            {
                await peer.GetStream().WriteAsync(wire.AsMemory(0, 2), token);
                await peer.GetStream().WriteAsync(wire.AsMemory(2), token);
                var prefix = await received.Reader.ReadAsync(token);
                Assert.Equal(PolEncryptionFixture.Seed(seed, !game), prefix.Data);
                var login = await received.Reader.ReadAsync(token);
                Assert.Equal(PolEncryptionFixture.PlainLogin(seed, game), login.Data);
                var trailing = await received.Reader.ReadAsync(token);
                Assert.Equal(game ? new byte[] { 0x73, 0x42 } : new byte[] { 0xA0, 0, 0 }, trailing.Data);
                byte[] reply = Convert.FromHexString("B900FF92D8");
                byte[] expected = game ? Convert.FromHexString("B30C59E409A0") : reply;
                if (game)
                {
                    Assert.True(sessions.TryGet(login.Connection.SessionId, out var session));
                    session.NetworkSession.EnableCompression();
                    var encryptedReference = Convert.FromHexString(vector.GetProperty("SendPrefix").GetString()!);
                    for (var i = 0; i < expected.Length; i++)
                    {
                        expected[i] ^= (byte)(encryptedReference[i] ^ i);
                    }
                }
                await login.Connection.SendAsync(reply, token);
                var actual = new byte[expected.Length];
                await peer.GetStream().ReadExactlyAsync(actual, token);
                Assert.Equal(expected, actual);
            }
        }
        finally
        {
            await network.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory, InlineData(false, false), InlineData(true, false), InlineData(false, true), InlineData(true, true)]
    public async Task InvalidEncryptedHandshake_ClosesSocketWithoutDispatchingFrames(bool game, bool matchingSentinels)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var registry = await ConnectionRegistryFixture.CreateAsync();
        var config = Config(NetworkEncryptionMode.Optional);
        var options = game ? UoNetworkOptionsFactory.CreateGame(config, PacketRegistry.Default, [])
            : UoNetworkOptionsFactory.CreateLogin(config, PacketRegistry.Default);
        var network = new NetworkService(options, registry.Service);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        network.DataReceived += (_, _) => Interlocked.Increment(ref frames);
        network.ConnectionClosed += (_, _) => closed.TrySetResult();
        try
        {
            await network.StartAsync();
            using var peer = new TcpClient();
            await peer.ConnectAsync(network.Listeners[0].Endpoint, token);
            var payload = Enumerable.Repeat((byte)0xAA, game ? 65 : 62).ToArray();
            if (matchingSentinels)
            {
                payload = PolEncryptionFixture.PlainLogin(0x12345678, game);
                payload[game ? 5 : 1] = 0xFF;
                payload = PolEncryptionFixture.EncryptModernLogin(payload, game);
            }
            byte[] wire = [.. PolEncryptionFixture.Seed(0x12345678, !game), .. payload];
            await peer.GetStream().WriteAsync(wire, token);
            await closed.Task.WaitAsync(token);
            Assert.Equal(0, Volatile.Read(ref frames));
        }
        finally
        {
            await network.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static MoongateServerConfig Config(NetworkEncryptionMode mode)
    {
        return new()
        {
            Network = new()
            {
                ListenAddress = "127.0.0.1", LoginPort = 0, GamePort = 0,
                Encryption = new() { Mode = mode, ClientVersion = "67.0.117.0" }
            }
        };
    }
}
