using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;

namespace Moongate.Tests.Support.Sessions;

public sealed class SessionFixture : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Socket _peer;

    public MoongateTcpClient Client { get; }

    public GameLoopService Loop { get; }

    private SessionFixture(MoongateTcpClient client, Socket peer, GameLoopService loop)
    {
        Client = client;
        _peer = peer;
        Loop = loop;
    }

    public static async Task<SessionFixture> CreateAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Socket? peer = null;
        MoongateTcpClient? client = null;
        GameLoopService? loop = null;

        try
        {
            var endPoint = (IPEndPoint)listener.LocalEndpoint;
            var acceptTask = listener.AcceptSocketAsync();
            client = await MoongateTcpClient.ConnectAsync(endPoint).WaitAsync(Timeout);
            peer = await acceptTask.WaitAsync(Timeout);
            loop = new GameLoopService(
                new GameLoopOptions { QueueCapacity = 16, MaxWorkItemsPerBatch = 4 },
                new TimerWheelService(new TimerWheelOptions(), TimeProvider.System),
                TimeProvider.System
            );
            await loop.StartAsync().WaitAsync(Timeout);

            return new SessionFixture(client, peer, loop);
        }
        catch
        {
            loop?.Dispose();
            peer?.Dispose();
            if (client is not null)
            {
                await client.DisposeAsync();
            }
            throw;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task ExecuteOnLoopAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Loop.PostAsync(new SessionGameLoopWorkItem(action, completion));
        await completion.Task.WaitAsync(Timeout);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        _peer.Dispose();
        await Loop.StopAsync().WaitAsync(Timeout);
        Loop.Dispose();
    }
}
