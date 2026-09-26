using System.Threading.Channels;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Data.GameLoop.Internal;

namespace Moongate.Server.Services.GameLoop.Internal;

/// <summary>
///     Runs bounded synchronous handlers on its caller's thread; the service owns failure policy.
/// </summary>
internal sealed class GameLoopPump
{
    private readonly ChannelReader<QueuedGameLoopWorkItem> _reader;
    private readonly int _maxWorkItemsPerBatch;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _workItemBudget;
    private readonly object _metricsGate = new();
    private long _executedWorkItems;
    private TimeSpan _lastBatchDuration;
    private TimeSpan _maxHandlerDuration;

    internal GameLoopPump(
        ChannelReader<QueuedGameLoopWorkItem> reader,
        int maxWorkItemsPerBatch,
        TimeProvider timeProvider,
        TimeSpan workItemBudget
    )
    {
        _reader = reader;
        _maxWorkItemsPerBatch = maxWorkItemsPerBatch;
        _timeProvider = timeProvider;
        _workItemBudget = workItemBudget;
    }

    internal GameLoopMetricsSnapshot GetMetricsSnapshot()
    {
        lock (_metricsGate)
        {
            return new()
            {
                ExecutedWorkItems = _executedWorkItems,
                LastBatchDuration = _lastBatchDuration,
                MaxHandlerDuration = _maxHandlerDuration
            };
        }
    }

    internal int RunBatch()
    {
        var attempted = 0;
        var batchStarted = _timeProvider.GetTimestamp();
        var handlerFaulted = false;

        try
        {
            // Both budgets are checked before dequeue: the next batch retains every queued item.
            while (attempted < _maxWorkItemsPerBatch &&
                   (attempted == 0 || _timeProvider.GetElapsedTime(batchStarted) < _workItemBudget) &&
                   _reader.TryRead(out var workItem))
            {
                attempted++;
                var handlerStarted = _timeProvider.GetTimestamp();

                lock (_metricsGate)
                {
                    _executedWorkItems++;
                }

                try
                {
                    workItem.WorkItem.Execute();
                }
                catch
                {
                    handlerFaulted = true;

                    throw;
                }
                finally
                {
                    var duration = GetDiagnosticElapsedTime(handlerStarted, handlerFaulted);

                    lock (_metricsGate)
                    {
                        if (duration > _maxHandlerDuration)
                        {
                            _maxHandlerDuration = duration;
                        }
                    }
                }
            }

            return attempted;
        }
        finally
        {
            if (attempted > 0)
            {
                var duration = GetDiagnosticElapsedTime(batchStarted, handlerFaulted);

                lock (_metricsGate)
                {
                    _lastBatchDuration = duration;
                }
            }
        }
    }

    private TimeSpan GetDiagnosticElapsedTime(long startedAt, bool handlerFaulted)
    {
        try
        {
            return _timeProvider.GetElapsedTime(startedAt);
        }
        catch (Exception) when (handlerFaulted)
        {
            // Diagnostic failures cannot replace the original command exception.
            return TimeSpan.Zero;
        }
    }
}
