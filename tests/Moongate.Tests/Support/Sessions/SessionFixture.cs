using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Moongate.Network.Client;
using Moongate.Server.Services.GameLoop;

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
            loop = new(
                new() { QueueCapacity = 16, MaxWorkItemsPerBatch = 4 },
                new(new(), TimeProvider.System),
                TimeProvider.System
            );
            await loop.StartAsync().WaitAsync(Timeout);

            return new(client, peer, loop);
        }
        catch (Exception creationException)
        {
            try
            {
                await DisposeResourcesAsync(client, peer, loop);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(creationException, cleanupException);
            }

            throw;
        }
        finally
        {
            listener.Stop();
        }
    }

    public async ValueTask DisposeAsync()
        => await DisposeResourcesAsync(Client, _peer, Loop);

    public async Task ExecuteOnLoopAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Loop.PostAsync(new SessionGameLoopWorkItem(action, completion));
        await completion.Task.WaitAsync(Timeout);
    }

    private static void Attempt(Action action, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static async Task<bool> AttemptAsync(Func<Task> action, List<Exception> failures)
    {
        try
        {
            await action();

            return true;
        }
        catch (Exception exception)
        {
            failures.Add(exception);

            return false;
        }
    }

    private static async Task DisposeResourcesAsync(
        MoongateTcpClient? client,
        Socket? peer,
        GameLoopService? loop
    )
    {
        var failures = new List<Exception>();

        try
        {
            if (client is not null)
            {
                await AttemptAsync(() => client.DisposeAsync().AsTask().WaitAsync(Timeout), failures);
            }
        }
        finally
        {
            try
            {
                if (peer is not null)
                {
                    Attempt(peer.Dispose, failures);
                }
            }
            finally
            {
                if (loop is not null)
                {
                    var stopped = false;

                    try
                    {
                        stopped = await AttemptAsync(() => loop.StopAsync().WaitAsync(Timeout), failures);
                    }
                    finally
                    {
                        if (stopped)
                        {
                            Attempt(loop.Dispose, failures);
                        }
                    }
                }
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }
}
