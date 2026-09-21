using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("settable")]
public sealed class SettableConstantModule
{
    [ScriptConstant] public static int Mutable { get; set; } = 1;
}
