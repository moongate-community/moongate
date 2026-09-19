using Moongate.Scripting.Attributes.Scripts;
using Serilog;
using Serilog.Events;

namespace Moongate.Scripting.Modules;

/// <summary>Structured logging for scripts. The message is a Serilog template; extra arguments fill its holes.</summary>
[ScriptModule("log", "Writes to the server log.")]
public sealed class LogModule
{
    private readonly ILogger _logger = Log.ForContext<LogModule>();

    /// <summary>Numeric value of the Debug level.</summary>
    [ScriptConstant("LEVEL_DEBUG")]
    public static readonly int LevelDebug = (int)LogEventLevel.Debug;

    /// <summary>Numeric value of the Information level.</summary>
    [ScriptConstant("LEVEL_INFO")]
    public static readonly int LevelInfo = (int)LogEventLevel.Information;

    /// <summary>Numeric value of the Warning level.</summary>
    [ScriptConstant("LEVEL_WARNING")]
    public static readonly int LevelWarning = (int)LogEventLevel.Warning;

    /// <summary>Numeric value of the Error level.</summary>
    [ScriptConstant("LEVEL_ERROR")]
    public static readonly int LevelError = (int)LogEventLevel.Error;

    /// <summary>Writes a Debug event; <paramref name="message"/> is a Serilog template filled by <paramref name="args"/>.</summary>
    [ScriptFunction(helpText: "Logs at DEBUG.")]
    public void Debug(string message, params object?[] args)
    {
        Write(LogEventLevel.Debug, message, args);
    }

    /// <summary>Writes an Information event; <paramref name="message"/> is a Serilog template filled by <paramref name="args"/>.</summary>
    [ScriptFunction(helpText: "Logs at INFO.")]
    public void Info(string message, params object?[] args)
    {
        Write(LogEventLevel.Information, message, args);
    }

    /// <summary>Writes a Warning event; <paramref name="message"/> is a Serilog template filled by <paramref name="args"/>.</summary>
    [ScriptFunction(helpText: "Logs at WARNING.")]
    public void Warning(string message, params object?[] args)
    {
        Write(LogEventLevel.Warning, message, args);
    }

    /// <summary>Writes an Error event; <paramref name="message"/> is a Serilog template filled by <paramref name="args"/>.</summary>
    [ScriptFunction(helpText: "Logs at ERROR.")]
    public void Error(string message, params object?[] args)
    {
        Write(LogEventLevel.Error, message, args);
    }

    private void Write(LogEventLevel level, string message, object?[] args)
    {
        // Templates come from scripts, so a malformed one must not become a server exception.
#pragma warning disable CA2254
        _logger.Write(level, message, args);
#pragma warning restore CA2254
    }
}
