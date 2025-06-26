using Moongate.Core.Server.Attributes.Scripts;

namespace Moongate.Server.Modules;

[ScriptModule("console")]
public class ConsoleModule
{

    [ScriptFunction("Log info")]
    public void Log(string message, params object[] args)
    {
        Serilog.Log.Information(message, args);
    }

}
