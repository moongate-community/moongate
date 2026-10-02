using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Moongate.Server.Services.Logging.Internal;

/// <summary>
///     Formats each event into a text writer, the console lines' stand-in where there is no terminal to write to.
/// </summary>
internal sealed class TextWriterLogSink : ILogEventSink
{
    private readonly Lock _sync = new();
    private readonly ITextFormatter _formatter;
    private readonly TextWriter _writer;

    public TextWriterLogSink(ITextFormatter formatter, TextWriter writer)
    {
        _formatter = formatter;
        _writer = writer;
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_sync)
        {
            _formatter.Format(logEvent, _writer);
            _writer.Flush();
        }
    }
}
