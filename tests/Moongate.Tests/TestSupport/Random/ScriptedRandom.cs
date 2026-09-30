namespace Moongate.Tests.TestSupport.Random;

/// <summary>
///     Returns the given values from <see cref="Next(int, int)" /> in order, each clamped into the asked range; the last
///     one repeats once they run out.
/// </summary>
public sealed class ScriptedRandom : System.Random
{
    private readonly Queue<int> _values;
    private int _last;

    public ScriptedRandom(params int[] values)
    {
        _values = new(values);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (_values.Count > 0)
        {
            _last = _values.Dequeue();
        }

        return Math.Clamp(_last, minValue, maxValue - 1);
    }
}
