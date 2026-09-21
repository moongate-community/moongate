using System.Net.Sockets;
using System.Security.Authentication;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Tests.TestSupport.Contracts;
using Moongate.Api.Tests.TestSupport.Handlers;
using Moongate.Api.Tests.TestSupport.Hosting;

namespace Moongate.Api.Tests.Integration.Hosting;

public class ApiServerAdmissionTests
{
    [Fact]
    public async Task DisconnectedNonCooperativeHandler_RetainsAdmissionBeforeTlsUntilApiCompletion()
    {
        var handler = new GatedHandler(false);
        await using var pair = await ApiPair.StartAsync(
                                   handler,
                                   new()
                                   {
                                       MaxConnections = 1,
                                       MaxConcurrentHandlers = 2,
                                       HandlerTimeout = TimeSpan.FromSeconds(30)
                                   }
                               );
        var pending = pair.ClientConnection.RequestAsync<IncrementRequest, IncrementResponse>(new());
        await handler.Entered.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            await pair.ClientConnection.CloseAsync();
            await Assert.ThrowsAsync<IOException>(() => pending);
            await pair.ClientConnection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pair.ServerConnection.Completion.IsCompleted);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var raw = new TcpClient();
                await raw.ConnectAsync(pair.Server.Endpoint!);

                // Admission must reject before TLS, even when the newcomer sends no ClientHello.
                // The handshake deadline is 5 seconds; observing EOF within 2 seconds proves rejection.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                try
                {
                    Assert.Equal(0, await raw.GetStream().ReadAsync(new byte[1], timeout.Token));
                }
                catch (IOException) { }

                await Task.Delay(30);
            }

            Assert.Equal(1, handler.Count);
            handler.Release.SetResult();
            await pair.ServerConnection.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await using var replacement = await ConnectEventuallyAsync(pair);
            Assert.Equal(
                42,
                (await replacement.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 }))
                .Value
            );
        }
        finally
        {
            handler.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task FailedTlsSetup_ReleasesAdmissionForNextValidConnection()
    {
        await using var pair = await ApiPair.StartAsync(options: new() { MaxConnections = 1 });
        await pair.ClientConnection.CloseAsync();
        await Task.WhenAll(pair.ClientConnection.Completion, pair.ServerConnection.Completion)
                  .WaitAsync(TimeSpan.FromSeconds(5));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var error = await Record.ExceptionAsync(
                            () => pair.Client.ConnectAsync(
                                pair.Server.Endpoint!,
                                "localhost",
                                "wrong-peer"
                            )
                        );
            Assert.True(error is AuthenticationException or IOException, error?.ToString());
        }

        await using var replacement = await ConnectEventuallyAsync(pair);
        Assert.Equal(
            42,
            (await replacement.RequestAsync<IncrementRequest, IncrementResponse>(new() { Value = 41 })).Value
        );
    }

    private static async Task<IApiConnection> ConnectEventuallyAsync(ApiPair pair)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (true)
        {
            try
            {
                return await pair.Client.ConnectAsync(pair.Server.Endpoint!, "localhost", "game", timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or AuthenticationException)
            {
                await Task.Delay(10, timeout.Token);
            }
        }
    }
}
