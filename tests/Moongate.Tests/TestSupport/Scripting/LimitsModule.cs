using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("limits")]
public sealed class LimitsModule
{
    [ScriptConstant] public static readonly int MAX_PLAYERS = 250;

    [ScriptConstant("version")] public static string Version => "1.2.3";

    [ScriptConstant] public static readonly double RATIO = 0.5;

    [ScriptConstant] public static readonly bool DEBUG = true;

    [ScriptConstant] public static readonly ProbeColour DEFAULT_COLOUR = ProbeColour.Green;
}
