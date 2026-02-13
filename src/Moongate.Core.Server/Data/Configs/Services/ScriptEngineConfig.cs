using Moongate.Core.Server.Types;

namespace Moongate.Core.Data.Configs.Services;

public class ScriptEngineConfig
{
    public List<string> InitScriptsFileNames { get; set; } = new() { "bootstrap.js", "init.js" };

    public ScriptNameConversion ScriptNameConversion { get; set; } = ScriptNameConversion.CamelCase;
}
