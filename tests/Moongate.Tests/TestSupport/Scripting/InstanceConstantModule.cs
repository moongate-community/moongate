using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("instance")]
public sealed class InstanceConstantModule
{
    [ScriptConstant] public int NotStatic = 1;
}
