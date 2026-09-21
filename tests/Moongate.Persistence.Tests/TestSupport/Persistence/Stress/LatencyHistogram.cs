namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

/// <summary>Bounded one-millisecond buckets; overflow uses the observed maximum instead of clipping slow calls.</summary>
internal sealed class LatencyHistogram
{
    private readonly long[] _buckets = new long[60_002];
    private readonly Lock _gate = new();
    private long _count;
    private double _maximum;

    public long Count => _count;

    public void Record(double milliseconds)
    {
        lock (_gate)
        {
            _buckets[(int)Math.Min(Math.Ceiling(milliseconds), _buckets.Length - 1)]++;
            _count++;
            _maximum = Math.Max(_maximum, milliseconds);
        }
    }

    public double Percentile(double rank)
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                return 0;
            }

            var target = (long)Math.Ceiling(_count * rank);
            long seen = 0;
            for (var index = 0; index < _buckets.Length; index++)
            {
                seen += _buckets[index];
                if (seen >= target)
                {
                    return index == _buckets.Length - 1 ? _maximum : index;
                }
            }

            return _maximum;
        }
    }
}
