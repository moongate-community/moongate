using Moongate.Server.Interfaces.Internal.Console;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.TestSupport.Console;

internal sealed class RecordingLogEventSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    private readonly IConsoleDriver _driver;

    public IReadOnlyList<LogEvent> Events => _events;

    public RecordingLogEventSink(IConsoleDriver driver)
    {
        _driver = driver;
    }

    public void Emit(LogEvent logEvent)
    {
        _events.Add(logEvent);
        _driver.WriteLine("emitted");
    }
}
