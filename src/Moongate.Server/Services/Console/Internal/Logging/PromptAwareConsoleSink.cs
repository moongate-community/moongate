using Moongate.Server.Core.Interfaces.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Server.Services.Console.Internal.Logging;

/// <summary>Delivers Serilog events to an inner console logger while the prompt row is hidden.</summary>
internal sealed class PromptAwareConsoleSink : ILogEventSink, IDisposable
{
    private readonly IConsolePromptService _prompt;
    private readonly ILogger _inner;

    public PromptAwareConsoleSink(IConsolePromptService prompt, ILogger inner)
    {
        _prompt = prompt;
        _inner = inner;
    }

    public void Emit(LogEvent logEvent)
    {
        _prompt.RunWithPromptHidden(() => _inner.Write(logEvent));
    }

    public void Dispose()
    {
        (_inner as IDisposable)?.Dispose();
    }
}
