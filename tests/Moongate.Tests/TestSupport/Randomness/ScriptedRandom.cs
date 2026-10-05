namespace Moongate.Tests.TestSupport.Randomness;

/// <summary>
///     A random that gives the numbers a test queued, in order, then <see cref="Rest" />: a roll a test decides.
/// </summary>
public sealed class ScriptedRandom : System.Random
{
    private readonly Queue<double> _doubles = new();
    private readonly Queue<int> _integers = new();

    /// <summary>
    ///     What <see cref="NextDouble" /> gives once the queue is empty.
    /// </summary>
    public double Rest { get; set; } = 0.999;

    /// <summary>
    ///     How many times <see cref="NextDouble" /> was asked.
    /// </summary>
    public int Rolls { get; private set; }

    public ScriptedRandom Doubles(params double[] values)
    {
        foreach (var value in values)
        {
            _doubles.Enqueue(value);
        }

        return this;
    }

    public ScriptedRandom Integers(params int[] values)
    {
        foreach (var value in values)
        {
            _integers.Enqueue(value);
        }

        return this;
    }

    public override double NextDouble()
    {
        Rolls++;

        return _doubles.TryDequeue(out var value) ? value : Rest;
    }

    public override int Next(int maxValue)
    {
        return _integers.TryDequeue(out var value) ? value : 0;
    }
}
