namespace Moongate.Sample.Plugin.Internal;

/// <summary>Counts greetings. The module increments it on the game loop; the metric provider reads it from the diagnostics thread, so the counter is atomic.</summary>
public sealed class GreetingCounter
{
    private long _count;

    /// <summary>Gets the number of greetings produced since the plugin was registered.</summary>
    public long Count => Interlocked.Read(ref _count);

    /// <summary>Records one greeting.</summary>
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
}
