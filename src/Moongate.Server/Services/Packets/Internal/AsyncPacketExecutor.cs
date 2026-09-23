using System.Threading.Channels;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Serilog;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class AsyncPacketExecutor : IAsyncDisposable
{
    private const int WorkerCount = 4;
    private const int MaxInFlight = 64;

    private readonly Lock _gate = new();
    private readonly Dictionary<long, AsyncPacketJob> _jobs = new();
    private readonly Channel<AsyncPacketJob> _queue = Channel.CreateUnbounded<AsyncPacketJob>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false, AllowSynchronousContinuations = false }
    );
    private readonly CancellationTokenSource _stopping = new();
    private readonly IGameLoopService _gameLoop;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ILogger _logger = Log.ForContext<AsyncPacketExecutor>();
    private readonly Task[] _workers;
    private bool _accepting = true;

    public AsyncPacketExecutor(IGameLoopService gameLoop, ISessionService sessions, IPacketSendService sender)
    {
        _gameLoop = gameLoop;
        _sessions = sessions;
        _sender = sender;
        _workers = Enumerable.Range(0, WorkerCount).Select(_ => RunWorkerAsync()).ToArray();
    }

    public bool IsBusy(long sessionId)
    {
        lock (_gate)
        {
            return _jobs.ContainsKey(sessionId);
        }
    }

    public bool TryReserve(
        GameSession session,
        IPacket packet,
        Func<PacketContext, IPacket, CancellationToken, ValueTask> handler,
        out AsyncPacketJob? job
    )
    {
        lock (_gate)
        {
            if (!_accepting || _jobs.Count >= MaxInFlight || _jobs.ContainsKey(session.SessionId))
            {
                job = null;
                return false;
            }

            job = new AsyncPacketJob(session, packet, handler, _stopping.Token);
            _jobs.Add(session.SessionId, job);
            return true;
        }
    }

    public bool TryEnqueue(AsyncPacketJob job)
    {
        lock (_gate)
        {
            if (!_accepting ||
                !_jobs.TryGetValue(job.Session.SessionId, out var current) ||
                !ReferenceEquals(current, job))
            {
                return false;
            }

            if (!_queue.Writer.TryWrite(job))
            {
                return false;
            }

            job.Enqueued = true;
            return true;
        }
    }

    public void CancelSession(long sessionId)
    {
        AsyncPacketJob? job;
        lock (_gate)
        {
            if (!_jobs.TryGetValue(sessionId, out job))
            {
                return;
            }

        }

        try
        {
            job.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        lock (_gate)
        {
            if (!job.Enqueued)
            {
                ReleaseCore(job);
            }
        }
    }

    public void Release(AsyncPacketJob job)
    {
        lock (_gate)
        {
            ReleaseCore(job);
        }
    }

    public async Task StopAsync()
    {
        AsyncPacketJob[] jobs;

        lock (_gate)
        {
            if (!_accepting)
            {
                return;
            }

            _accepting = false;
            _queue.Writer.TryComplete();
            jobs = _jobs.Values.ToArray();
        }

        await _stopping.CancelAsync().ConfigureAwait(false);

        foreach (var job in jobs.Where(job => !job.Enqueued))
        {
            Release(job);
        }

        await Task.WhenAll(_workers).ConfigureAwait(false);
    }

    private void ReleaseCore(AsyncPacketJob job)
    {
        if (_jobs.TryGetValue(job.Session.SessionId, out var current) && ReferenceEquals(current, job))
        {
            _jobs.Remove(job.Session.SessionId);
            job.Cancellation.Dispose();
        }
    }

    private async Task RunWorkerAsync()
    {
        await foreach (var job in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (!job.Cancellation.IsCancellationRequested)
                {
                    var context = new PacketContext(job.Session, _gameLoop, _sessions, _sender);
                    await job.Handler(context, job.Packet, job.Cancellation.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
            {
                // Disconnect or shutdown canceled this request.
            }
            catch (Exception exception)
            {
                _logger.Error(
                    exception,
                    "Async packet handler failed for session {SessionId}, packet {PacketType}",
                    job.Session.SessionId,
                    job.Packet.GetType().Name
                );
            }
            finally
            {
                Release(job);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stopping.Dispose();
    }
}
