using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Tests.Support;

/// <summary>Captures events from components that create contextual loggers from <see cref="Log.Logger" />.</summary>
public sealed class GlobalSerilogCapture : IDisposable
{
    private readonly CaptureSink _sink = new();
    private readonly ILogger _logger;
    private readonly ILogger _previous;

    private bool _disposed;

    public IReadOnlyList<LogEvent> Events => _sink.Events;

    public GlobalSerilogCapture()
    {
        _previous = Log.Logger;
        _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(_sink)
            .CreateLogger();
        Log.Logger = _logger;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Log.Logger = _previous;
        if (_logger is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    private sealed class CaptureSink : ILogEventSink
    {
        private readonly Lock _sync = new();
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToArray();
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_sync)
            {
                _events.Add(logEvent);
            }
        }
    }
}
