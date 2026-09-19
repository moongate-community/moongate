using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>Keeps every Serilog event it receives so a test can assert on what a script logged.</summary>
internal sealed class CapturingLogSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToArray();
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_events)
        {
            _events.Add(logEvent);
        }
    }
}
