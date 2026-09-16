using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using Moongate.Network.Server;
using Moongate.Network.Tests.Support;

using Xunit.Abstractions;

namespace Moongate.Network.Tests.Performance;

[Collection("TCP performance")]
public sealed class TcpThroughputTests
{
    private readonly ITestOutputHelper _output;

    public TcpThroughputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory, Trait("Category", "Performance"), InlineData(1, 32), InlineData(32, 128), InlineData(128, 240)]
    public async Task Receive_ConcurrentClients_DeliversEveryFrame(int connections, int payloadSize)
    {
        const int messagesPerClient = 256;
        var expectedCount = connections * messagesPerClient;
        var frame = new byte[payloadSize + 1];
        frame[0] = (byte)payloadSize;
        frame.AsSpan(1).Fill(0x5A);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var server = new MoongateTcpServer(
            new IPEndPoint(IPAddress.Loopback, 0), framer: new LengthPrefixFramer());
        server.OnDataReceived += (_, args) =>
        {
            if (!args.Data.Span.SequenceEqual(frame))
            {
                received.TrySetException(new InvalidDataException("Corrupted load-test frame."));
                return;
            }

            if (Interlocked.Increment(ref count) == expectedCount)
            {
                received.TrySetResult();
            }
        };
        var peers = new List<TcpClient>();
        await server.StartAsync(deadline.Token);

        try
        {
            for (var index = 0; index < connections; index++)
            {
                var peer = new TcpClient { NoDelay = true };
                peers.Add(peer);
                await peer.ConnectAsync(IPAddress.Loopback, server.Port, deadline.Token);
            }

            var before = GC.GetTotalAllocatedBytes(precise: true);
            var startedAt = Stopwatch.GetTimestamp();
            await Task.WhenAll(peers.Select(async peer =>
            {
                for (var index = 0; index < messagesPerClient; index++)
                {
                    await peer.GetStream().WriteAsync(frame, deadline.Token);
                }
            }));
            await received.Task.WaitAsync(deadline.Token);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
            Assert.Equal(expectedCount, Volatile.Read(ref count));
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                connections,
                payloadSize,
                frames = expectedCount,
                elapsedMilliseconds = elapsed.TotalMilliseconds,
                framesPerSecond = expectedCount / elapsed.TotalSeconds,
                allocatedBytesWholeProcess = allocated
            }));
        }
        finally
        {
            foreach (var peer in peers)
            {
                peer.Dispose();
            }

            await server.StopAsync(CancellationToken.None);
        }
    }
}
