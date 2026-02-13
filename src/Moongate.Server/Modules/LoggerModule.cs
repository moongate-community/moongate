using Moongate.Core.Server.Attributes.Scripts;
using Serilog;

namespace Moongate.Server.Modules;

[ScriptModule("logger")]
public class LoggerModule
{
    private readonly ILogger _logger = Log.ForContext<LoggerModule>();

    [ScriptFunction("Log debug")]
    public void Debug(string message, params object[] args)
    {
        _logger.Debug("[JS] " + message, args);
    }

    [ScriptFunction("Log error")]
    public void Error(string message, params object[] args)
    {
        _logger.Error("[JS] " + message, args);
    }

    [ScriptFunction("Log info")]
    public void Info(string message, params object[] args)
    {
        _logger.Information("[JS] " + message, args);
    }

    [ScriptFunction("Log warning")]
    public void Warn(string message, params object[] args)
    {
        _logger.Warning("[JS] " + message, args);
    }
}
