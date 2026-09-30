namespace Moongate.Tests.TestSupport.Ultima.Decorations;

/// <summary>
///     Keeps every reported value, in order, on the reporting thread.
/// </summary>
public sealed class RecordingProgress<T> : IProgress<T>
{
    public List<T> Reports { get; } = [];

    public void Report(T value)
    {
        Reports.Add(value);
    }
}
