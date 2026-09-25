namespace Moongate.Sample.Plugin.Internal;

/// <summary>
/// Counts greetings. Incremented from the game loop by the module and from the console thread by the command; read by
/// the diagnostics thread; hence atomic.
/// </summary>
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
