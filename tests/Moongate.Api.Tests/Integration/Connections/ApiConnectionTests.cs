using Moongate.Api.Exceptions;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Tests.TestSupport.Handlers;
using Moongate.Api.Tests.TestSupport.Hosting;

namespace Moongate.Api.Tests.Integration.Connections;

public class ApiConnectionTests
{
    [Fact]
    public async Task TypedCalls_WorkInBothDirectionsOverMutualTls()
    {
        await using var pair = await ApiPair.StartAsync();
        var reply = await pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(
            new IncrementRequest { Value = 41 }
        );
        Assert.Equal(42, reply.Value);
        var reverse =
            await pair.ServerConnection.RequestAsync<IncrementRequest, IncrementResponse>(
                new IncrementRequest { Value = 9 }
            );
        Assert.Equal(10, reverse.Value);
        Assert.Equal("game", pair.ClientConnection.Peer.PeerId);
        Assert.Equal("admin", pair.ServerConnection.Peer.PeerId);
    }

    [Fact]
    public async Task ConcurrentCalls_KeepIndependentCorrelationInBothDirections()
    {
        await using var pair = await ApiPair.StartAsync();
        var forward = Enumerable.Range(0, 16)
            .Select(value =>
                pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(
                    new IncrementRequest { Value = value }
                )
            )
            .ToArray();
        var reverse = Enumerable.Range(30, 16)
            .Select(value =>
                pair.ServerConnection.RequestAsync<IncrementRequest, IncrementResponse>(
                    new IncrementRequest { Value = value }
                )
            )
            .ToArray();
        Assert.Equal(Enumerable.Range(1, 16), (await Task.WhenAll(forward)).Select(result => result.Value));
        Assert.Equal(Enumerable.Range(31, 16), (await Task.WhenAll(reverse)).Select(result => result.Value));
    }

    [Fact]
    public async Task Disconnect_FailsPendingCallAndEventuallyRemovesSession()
    {
        var handler = new GatedHandler();
        await using var pair = await ApiPair.StartAsync(handler);
        var pending = pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(new IncrementRequest());
        await handler.Entered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var snapshot = pair.Server.Connections;
        await pair.ClientConnection.CloseAsync();
        await Assert.ThrowsAsync<IOException>(() => pending);
        await Task.WhenAll(pair.ClientConnection.Completion, pair.ServerConnection.Completion)
            .WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (pair.Server.Connections.Count != 0)
        {
            await Task.Delay(1, timeout.Token);
        }

        Assert.Single(snapshot);
        Assert.Empty(pair.Server.Connections);
    }
}
