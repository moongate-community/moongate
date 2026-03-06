using System.Diagnostics;
using Moongate.Abstractions.Services.Base;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Messaging;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Metrics.Data;
using Serilog;

namespace Moongate.Server.Services.EventLoop;

/// <summary>
/// Represents GameLoopService.
/// </summary>
public class GameLoopService : BaseMoongateService, IGameLoopService, IGameLoopMetricsSource, IDisposable
{
    private const int MaxInboundPacketsPerTick = 128;
    private const int MaxOutboundPacketsPerTick = 1024;
    private const double SlowTickThresholdMilliseconds = 250;
    private static readonly bool UseFastTimestampMath = Stopwatch.Frequency % 1000 == 0;
    private static readonly ulong FrequencyInMilliseconds = (ulong)Stopwatch.Frequency / 1000;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IMessageBusService _messageBusService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ITimerService _timerService;
    private readonly IOutboundPacketSender _outboundPacketSender;
    private readonly ILogger _logger = Log.ForContext<GameLoopService>();
    private readonly IPacketDispatchService _packetDispatchService;
    private readonly bool _idleCpuEnabled;
    private readonly int _idleSleepMilliseconds;
    private readonly Lock _metricsSync = new();
    private readonly CancellationTokenSource _outboundCancellationTokenSource = new();
    private readonly AutoResetEvent _outboundWorkSignal = new(false);

    private Thread? _loopThread;
    private Thread? _outboundThread;
    private long _tickCount;
    private TimeSpan _uptime;
    private double _averageTickMs;
    private double _maxTickMs;
    private long _idleSleepCount;
    private long _totalWorkUnits;
    private double _averageWorkUnits;
    private long _outboundPacketsTotal;

    public GameLoopService(
        IPacketDispatchService packetDispatchService,
        IMessageBusService messageBusService,
        IBackgroundJobService backgroundJobService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService,
        ITimerService timerService,
        IOutboundPacketSender outboundPacketSender,
        TimerServiceConfig? timerServiceConfig = null
    )
    {
        _packetDispatchService = packetDispatchService;
        _messageBusService = messageBusService;
        _backgroundJobService = backgroundJobService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
        _timerService = timerService;
        _outboundPacketSender = outboundPacketSender;
        _idleCpuEnabled = timerServiceConfig?.IdleCpuEnabled ?? true;
        _idleSleepMilliseconds = Math.Max(1, timerServiceConfig?.IdleSleepMilliseconds ?? 1);

        _logger.Information(
            "GameLoopService initialized. IdleCpu={IdleCpuEnabled} IdleSleepMs={IdleSleepMilliseconds}",
            _idleCpuEnabled,
            _idleSleepMilliseconds
        );
    }

    public void Dispose()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        if (!_outboundCancellationTokenSource.IsCancellationRequested)
        {
            _outboundCancellationTokenSource.Cancel();
        }

        _outboundWorkSignal.Set();
        _cancellationTokenSource.Dispose();
        _outboundCancellationTokenSource.Dispose();
        _outboundWorkSignal.Dispose();
        GC.SuppressFinalize(this);
    }

    public GameLoopMetricsSnapshot GetMetricsSnapshot()
    {
        lock (_metricsSync)
        {
            return new(
                _tickCount,
                _uptime,
                _averageTickMs,
                _maxTickMs,
                _idleSleepCount,
                _averageWorkUnits,
                _outgoingPacketQueue.CurrentQueueDepth,
                _outboundPacketsTotal
            );
        }
    }

    public override Task StartAsync()
    {
        _backgroundJobService.Start();
        _outboundThread = new(RunOutboundLoop)
        {
            IsBackground = true,
            Name = "Moongate-OutboundWriter"
        };
        _outboundThread.Start();
        _loopThread = new(RunLoop)
        {
            IsBackground = true,
            Name = "Moongate-GameLoop"
        };
        _loopThread.Start();

        return Task.CompletedTask;
    }

    public override async Task StopAsync()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        if (!_outboundCancellationTokenSource.IsCancellationRequested)
        {
            _outboundCancellationTokenSource.Cancel();
        }

        _outboundWorkSignal.Set();
        _loopThread?.Join();
        _outboundThread?.Join();

        await _backgroundJobService.StopAsync();
    }

    private int DrainOutgoingPacketQueue()
    {
        var drained = 0;

        while (drained < MaxOutboundPacketsPerTick && _outgoingPacketQueue.TryDequeue(out var outgoingPacket))
        {
            if (
                !_gameNetworkSessionService.TryGet(outgoingPacket.SessionId, out var session) ||
                session.NetworkSession.Client is not { } client
            )
            {
                // Hot path: per-packet logging here can dominate tick time under stress.
                continue;
            }

            _outboundPacketSender.Send(client, outgoingPacket);
            drained++;
            Interlocked.Increment(ref _outboundPacketsTotal);
        }

        return drained;
    }

    private int DrainPacketQueue()
    {
        var drained = 0;

        while (drained < MaxInboundPacketsPerTick && _messageBusService.TryReadIncomingPacket(out var gamePacket))
        {
            _packetDispatchService.NotifyPacketListeners(gamePacket);
            drained++;
        }

        return drained;
    }

    private static long GetTimestampMilliseconds()
    {
        if (UseFastTimestampMath)
        {
            return (long)((ulong)Stopwatch.GetTimestamp() / FrequencyInMilliseconds);
        }

        return (long)((UInt128)Stopwatch.GetTimestamp() * 1000 / (ulong)Stopwatch.Frequency);
    }

    private TickWorkBreakdown ProcessTick(long timestampMilliseconds)
    {
        var inboundStart = Stopwatch.GetTimestamp();
        var inbound = DrainPacketQueue();
        var inboundElapsed = Stopwatch.GetElapsedTime(inboundStart);

        var gameLoopCallbacksStart = Stopwatch.GetTimestamp();
        var gameLoopCallbacks = _backgroundJobService.ExecutePendingOnGameLoop();
        var gameLoopCallbacksElapsed = Stopwatch.GetElapsedTime(gameLoopCallbacksStart);

        var timerStart = Stopwatch.GetTimestamp();
        var timerTicks = _timerService.UpdateTicksDelta(timestampMilliseconds);
        var timerElapsed = Stopwatch.GetElapsedTime(timerStart);

        if (_outgoingPacketQueue.CurrentQueueDepth > 0)
        {
            _outboundWorkSignal.Set();
        }

        return new(
            inbound,
            gameLoopCallbacks,
            timerTicks,
            0,
            inboundElapsed,
            gameLoopCallbacksElapsed,
            timerElapsed,
            TimeSpan.Zero
        );
    }

    private void RunLoop()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            var tickStart = Stopwatch.GetTimestamp();
            var timestampMilliseconds = GetTimestampMilliseconds();

            var breakdown = ProcessTick(timestampMilliseconds);
            var workUnits = breakdown.TotalWorkUnits;

            var elapsed = Stopwatch.GetElapsedTime(tickStart);

            if (elapsed.TotalMilliseconds >= SlowTickThresholdMilliseconds)
            {
                _logger.Warning(
                    "Slow game tick: Total={TotalMs:0.###}ms Inbound={InboundMs:0.###}ms Background={BackgroundMs:0.###}ms Timer={TimerMs:0.###}ms Outbound={OutboundMs:0.###}ms WorkUnits={WorkUnits} (in={InboundCount}, bg={BackgroundCount}, timer={TimerCount}, out={OutboundCount}) Queues(in={InboundQueue}, out={OutboundQueue})",
                    elapsed.TotalMilliseconds,
                    breakdown.InboundElapsed.TotalMilliseconds,
                    breakdown.GameLoopCallbacksElapsed.TotalMilliseconds,
                    breakdown.TimerElapsed.TotalMilliseconds,
                    breakdown.OutboundElapsed.TotalMilliseconds,
                    workUnits,
                    breakdown.InboundCount,
                    breakdown.GameLoopCallbacksCount,
                    breakdown.TimerCount,
                    breakdown.OutboundCount,
                    _messageBusService.CurrentQueueDepth,
                    _outgoingPacketQueue.CurrentQueueDepth
                );

            }

            lock (_metricsSync)
            {
                _tickCount++;
                _uptime += elapsed;
                _averageTickMs = _averageTickMs * 0.95 + elapsed.TotalMilliseconds * 0.05;
                _maxTickMs = Math.Max(_maxTickMs, elapsed.TotalMilliseconds);
                _totalWorkUnits += workUnits;
                _averageWorkUnits = _tickCount == 0 ? 0 : (double)_totalWorkUnits / _tickCount;
            }

            if (_idleCpuEnabled && workUnits == 0)
            {
                Thread.Sleep(_idleSleepMilliseconds);
                Interlocked.Increment(ref _idleSleepCount);
            }
        }
    }

    private void RunOutboundLoop()
    {
        while (!_outboundCancellationTokenSource.IsCancellationRequested)
        {
            _outboundWorkSignal.WaitOne(_idleSleepMilliseconds);
            if (_outboundCancellationTokenSource.IsCancellationRequested)
            {
                break;
            }

            while (_outgoingPacketQueue.CurrentQueueDepth > 0)
            {
                var drained = DrainOutgoingPacketQueue();
                if (drained == 0)
                {
                    break;
                }
            }
        }
    }

    private readonly record struct TickWorkBreakdown(
        int InboundCount,
        int GameLoopCallbacksCount,
        int TimerCount,
        int OutboundCount,
        TimeSpan InboundElapsed,
        TimeSpan GameLoopCallbacksElapsed,
        TimeSpan TimerElapsed,
        TimeSpan OutboundElapsed
    )
    {
        public int TotalWorkUnits => InboundCount + GameLoopCallbacksCount + TimerCount + OutboundCount;
    }
}
