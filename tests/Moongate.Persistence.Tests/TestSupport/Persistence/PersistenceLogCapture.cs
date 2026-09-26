using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class PersistenceLogCapture : ILogEventSink, IDisposable
{
    private readonly ILogger _previous;
    private readonly Logger _logger;
    private bool _disposed;
    public ConcurrentQueue<LogEvent> Events { get; } = new();

    public PersistenceLogCapture()
    {
        _previous = Log.Logger;
        _logger = new LoggerConfiguration().WriteTo.Sink(this).CreateLogger();
        Log.Logger = _logger;
    }

    public void Emit(LogEvent logEvent)
    {
        Events.Enqueue(logEvent);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Log.Logger = _previous;
        _logger.Dispose();
    }
}
