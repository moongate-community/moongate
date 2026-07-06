using System.Net;
using System.Net.Sockets;
using System.Text;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces;
using Moongate.Server.Services;
using Moongate.Server.Services.Network;

namespace Moongate.Tests.Server;

public class LoginFlowIntegrationTests
{
    private static MoongateConfig LoopbackConfig()
    {
        return new MoongateConfig
        {
            ShardName = "Moongate",
            Network = new MoongateNetworkConfig { Address = "127.0.0.1", Port = 0, PublicAddress = "127.0.0.1" }
        };
    }

    private static async Task<NetworkService> StartServerAsync(MoongateConfig config)
    {
        var sessions = new SessionManager();
        var accounts = new StubAccountService();
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var handlers = new IPacketHandlerRegistration[]
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config)
        };

        var network = new NetworkService(sessions, config, handlers);
        await network.StartAsync(CancellationToken.None);

        return network;
    }

    private static byte[] AccountLogin(string account, string password)
    {
        var packet = new byte[62];
        packet[0] = 0x80;
        Encoding.ASCII.GetBytes(account).CopyTo(packet.AsSpan(1));
        Encoding.ASCII.GetBytes(password).CopyTo(packet.AsSpan(31));

        return packet;
    }

    private static byte[] ReadResponse(Socket socket, int expectedFirstByte)
    {
        var buffer = new byte[512];

        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (socket.Available > 0)
            {
                var read = socket.Receive(buffer);

                return buffer.AsSpan(0, read).ToArray();
            }

            Thread.Sleep(20);
        }

        throw new TimeoutException($"No response (expected 0x{expectedFirstByte:X2}).");
    }

    [Fact]
    public async Task FullLogin_SeedToRedirect_ReturnsServerListThenGameServer()
    {
        var config = LoopbackConfig();
        var network = await StartServerAsync(config);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A; // seed = 42
            socket.Send(seed);

            socket.Send(AccountLogin("squid", "secret"));
            var serverList = ReadResponse(socket, 0xA8);
            Assert.Equal(0xA8, serverList[0]);

            socket.Send(new byte[] { 0xA0, 0x00, 0x00 });
            var redirect = ReadResponse(socket, 0x8C);
            Assert.Equal(0x8C, redirect[0]);
            Assert.Equal(11, redirect.Length);
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsLoginDenied()
    {
        var config = LoopbackConfig();
        var network = await StartServerAsync(config);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A;
            socket.Send(seed);

            socket.Send(AccountLogin("squid", string.Empty));
            var denied = ReadResponse(socket, 0x82);

            Assert.Equal(0x82, denied[0]);
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }
}
