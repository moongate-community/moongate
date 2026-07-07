using Serilog;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Scripting.Modules;

[ScriptModule("log")]
public class LoggerModule
{
    private readonly ILogger _logger = Log.Logger.ForContext<LoggerModule>();

    [ScriptFunction("info", "Write information to console")]
    public void Info(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    [ScriptFunction("warn", "Write warning to console")]
    public void Warn(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    [ScriptFunction("error", "Write error to console")]
    public void Error(string message, params object[] args)
    {
        _logger.Error(message, args);
    }

    [ScriptFunction("debug", "Write debug information to console")]
    public void Debug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }
}
