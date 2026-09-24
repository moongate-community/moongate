namespace Moongate.Tests.Support.Events;

public sealed class RecordingDisposable : IDisposable
{
    private readonly List<string> _events;
    private readonly string _eventName;

    public RecordingDisposable(List<string> events, string eventName)
    {
        _events = events;
        _eventName = eventName;
    }

    public void Dispose()
        => _events.Add(_eventName);
}
