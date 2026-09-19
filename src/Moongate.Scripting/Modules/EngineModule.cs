using System.Reflection;
using Moongate.Core.Utils;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Scripting.Modules;

/// <summary>Constants describing the host. Registered first by the engine service; never removable.</summary>
[ScriptModule("engine", "Identifies the server running the script.")]
public sealed class EngineModule
{
    [ScriptConstant("name", "Always \"Moongate\".")]
    public static string Name => "Moongate";

    [ScriptConstant("version", "Semantic version of the running server.")]
    public static string Version => VersionUtils.GetVersion();

    [ScriptConstant("codename", "Release codename, or empty when the host declares none.")]
    public static string Codename => VersionUtils.GetCodename(Assembly.GetEntryAssembly() ?? typeof(EngineModule).Assembly);

    [ScriptConstant("platform", "Operating system platform name.")]
    public static string Platform => Environment.OSVersion.Platform.ToString();
}
