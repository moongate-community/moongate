using System.Net;
using System.Net.Sockets;
using Moongate.Server.Data.Network;
using Moongate.Server.Services.Network;

namespace Moongate.Tests.Server.Services.Network;

public sealed class PingServerServiceTests
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task StartAsync_ADatagram_ComesBackUnchanged()
    {
        var service = CreateService();
        await service.StartAsync();

        try
        {
            using var client = new UdpClient(AddressFamily.InterNetwork);
            byte[] ping = [0x01, 0x02, 0x03, 0x04];
            await client.SendAsync(ping, Assert.Single(service.LocalEndpoints));

            var answer = await client.ReceiveAsync().WaitAsync(AnswerTimeout);

            Assert.Equal(ping, answer.Buffer);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_ADatagramOverTheLimit_GetsNoAnswerAndTheNextOneDoes()
    {
        var service = CreateService(maxDatagramSize: 8);
        await service.StartAsync();

        try
        {
            using var client = new UdpClient(AddressFamily.InterNetwork);
            var endpoint = Assert.Single(service.LocalEndpoints);
            await client.SendAsync(new byte[9], endpoint);

            var answer = client.ReceiveAsync();

            await Assert.ThrowsAsync<TimeoutException>(() => answer.WaitAsync(SilenceTimeout));

            // The same pending receive now gets the answer to a datagram exactly at the limit.
            byte[] ping = [1, 2, 3, 4, 5, 6, 7, 8];
            await client.SendAsync(ping, endpoint);

            Assert.Equal(ping, (await answer.WaitAsync(AnswerTimeout)).Buffer);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Theory,
     InlineData(0, 40000, true),
     InlineData(64, 40000, true),
     InlineData(65, 40000, false),
     InlineData(4, 12000, false)]
    public void IsAnswered_DropsADatagramOverTheLimitOrFromAnotherPingServer(int size, int senderPort, bool expected)
    {
        // A datagram from the ping port of another host is an echo: answering it would bounce it back and forth forever.
        Assert.Equal(expected, PingServerService.IsAnswered(size, senderPort, localPort: 12000, maxDatagramSize: 64));
    }

    [Fact]
    public async Task StopAsync_Twice_DoesNotThrow()
    {
        var service = CreateService();
        await service.StartAsync();

        await service.StopAsync();
        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Disabled_BindsNothing()
    {
        var service = new PingServerService(
            new PingServerOptions { Enabled = false, Endpoints = [new IPEndPoint(IPAddress.Loopback, 0)] }
        );

        await service.StartAsync();

        Assert.Empty(service.LocalEndpoints);
        await service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_PortInUse_StartsWithoutThatEndpoint()
    {
        using var taken = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ExclusiveAddressUse = true
        };
        taken.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var service = new PingServerService(
            new PingServerOptions { Endpoints = [(IPEndPoint)taken.LocalEndPoint!, new IPEndPoint(IPAddress.Loopback, 0)] }
        );

        await service.StartAsync();

        try
        {
            Assert.Single(service.LocalEndpoints);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_AfterAStart_FreesThePort()
    {
        var service = CreateService();
        await service.StartAsync();
        var endpoint = Assert.Single(service.LocalEndpoints);

        await service.StopAsync();

        Assert.Empty(service.LocalEndpoints);
        using var again = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            ExclusiveAddressUse = true
        };
        again.Bind(endpoint);
    }

    [Fact]
    public async Task StopAsync_WithoutAStart_DoesNotThrow()
    {
        await CreateService().StopAsync();
    }

    private static PingServerService CreateService(int maxDatagramSize = 64)
    {
        return new(
            new PingServerOptions
            {
                Endpoints = [new IPEndPoint(IPAddress.Loopback, 0)],
                MaxDatagramSize = maxDatagramSize
            }
        );
    }
}
