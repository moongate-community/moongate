using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Tests.TestSupport.Streams;

namespace Moongate.Network.Tests.Integration.Client;

public sealed class ConfiguredConnectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ConnectConfiguredAsync_CancelDuringPreparation_ClosesPeer()
    {
        using var listener = Listen();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connecting = MoongateTcpClient.ConnectConfiguredAsync(
            (IPEndPoint)listener.LocalEndPoint!,
            new()
            {
                Pipeline = new()
                {
                    PrepareStreamAsync = async (stream, token) =>
                                         {
                                             entered.TrySetResult();
                                             await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);

                                             return stream;
                                         }
                }
            },
            cancellation.Token
        );
        using var peer = await listener.AcceptAsync();
        await entered.Task.WaitAsync(Timeout);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WaitAsync(Timeout));
        Assert.Equal(0, await peer.ReceiveAsync(new byte[1], SocketFlags.None).WaitAsync(Timeout));
    }

    [Fact]
    public async Task ConnectConfiguredAsync_PeerSendsImmediately_DeliversFirstByte()
    {
        using var listener = Listen();
        var received = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connecting = MoongateTcpClient.ConnectConfiguredAsync(
            (IPEndPoint)listener.LocalEndPoint!,
            new()
            {
                Pipeline = new()
                {
                    ConfigureClient = client =>
                                          client.OnDataReceived += (_, args) => received.TrySetResult(args.Data.Span[0])
                }
            }
        );
        using var peer = await listener.AcceptAsync();
        await peer.SendAsync(new byte[] { 42 }, SocketFlags.None);
        await using var client = await connecting.WaitAsync(Timeout);
        Assert.Equal((byte)42, await received.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task ConnectConfiguredAsync_PreparationDeadline_FailsWithoutConfiguringClient()
    {
        using var listener = Listen();
        var configured = false;
        var connecting = MoongateTcpClient.ConnectConfiguredAsync(
            (IPEndPoint)listener.LocalEndPoint!,
            new()
            {
                PreparationTimeout = TimeSpan.FromMilliseconds(50),
                Pipeline = new()
                {
                    PrepareStreamAsync = async (stream, token) =>
                                         {
                                             await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);

                                             return stream;
                                         },
                    ConfigureClient = _ => configured = true
                }
            }
        );
        using var peer = await listener.AcceptAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => connecting.WaitAsync(Timeout));
        Assert.False(configured);
    }

    [Fact]
    public async Task ConnectConfiguredAsync_PreparationThrows_ClosesUnderlyingStreamAndSocket()
    {
        using var listener = Listen();
        Stream? input = null;
        var connecting = MoongateTcpClient.ConnectConfiguredAsync(
            (IPEndPoint)listener.LocalEndPoint!,
            new()
            {
                Pipeline = new()
                {
                    PrepareStreamAsync = (stream, _) =>
                                         {
                                             input = stream;

                                             return ValueTask.FromException<Stream>(new IOException("preparation failed"));
                                         }
                }
            }
        );
        using var peer = await listener.AcceptAsync();
        await Assert.ThrowsAsync<IOException>(() => connecting.WaitAsync(Timeout));
        Assert.NotNull(input);
        Assert.False(input.CanRead);
        Assert.Equal(0, await peer.ReceiveAsync(new byte[1], SocketFlags.None).WaitAsync(Timeout));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ConnectConfiguredAsync_WrappedStream_DisposesOnce(bool configurationThrows)
    {
        using var listener = Listen();
        TrackingStream? wrapper = null;
        var connecting = MoongateTcpClient.ConnectConfiguredAsync(
            (IPEndPoint)listener.LocalEndPoint!,
            new()
            {
                Pipeline = new()
                {
                    PrepareStreamAsync = (stream, _) => ValueTask.FromResult<Stream>(wrapper = new(stream)),
                    ConfigureClient = _ =>
                                      {
                                          if (configurationThrows)
                                          {
                                              throw new IOException("configuration failed");
                                          }
                                      }
                }
            }
        );
        using var peer = await listener.AcceptAsync();

        if (configurationThrows)
        {
            await Assert.ThrowsAsync<IOException>(() => connecting.WaitAsync(Timeout));
        }
        else
        {
            var client = await connecting.WaitAsync(Timeout);
            await client.SendAsync(new byte[] { 7 }, CancellationToken.None);
            var buffer = new byte[1];
            Assert.Equal(1, await peer.ReceiveAsync(buffer, SocketFlags.None));
            Assert.Equal((byte)7, buffer[0]);
            await client.DisposeAsync();
        }

        Assert.NotNull(wrapper);
        Assert.Equal(1, wrapper.DisposeCount);
    }

    private static Socket Listen()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(4);

        return listener;
    }
}
