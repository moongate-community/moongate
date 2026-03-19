using Moongate.Server.Metrics.Data;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Tracks aggregate Lua brain tick metrics.
/// </summary>
internal sealed class LuaBrainMetricsTracker
{
    private readonly Lock _sync = new();
    private long _dueBrainsTotal;
    private long _processedBrainsTotal;
    private long _deferredBrainsTotal;
    private long _processedTicksTotal;
    private double _tickDurationTotalMilliseconds;
    private double _tickDurationMaxMilliseconds;

    public LuaBrainMetricsSnapshot CreateSnapshot()
    {
        lock (_sync)
        {
            var averageTickMilliseconds = _processedTicksTotal == 0
                                              ? 0
                                              : _tickDurationTotalMilliseconds / _processedTicksTotal;

            return new(
                _dueBrainsTotal,
                _processedBrainsTotal,
                _deferredBrainsTotal,
                _processedTicksTotal,
                _tickDurationTotalMilliseconds,
                averageTickMilliseconds,
                _tickDurationMaxMilliseconds
            );
        }
    }

    public void RecordTick(int dueCount, int processedCount, double elapsedMilliseconds)
    {
        var deferredCount = Math.Max(0, dueCount - processedCount);

        lock (_sync)
        {
            _dueBrainsTotal += dueCount;
            _processedBrainsTotal += processedCount;
            _deferredBrainsTotal += deferredCount;
            _processedTicksTotal++;
            _tickDurationTotalMilliseconds += elapsedMilliseconds;

            if (elapsedMilliseconds > _tickDurationMaxMilliseconds)
            {
                _tickDurationMaxMilliseconds = elapsedMilliseconds;
            }
        }
    }
}
