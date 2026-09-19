using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("broken")]
public sealed class BrokenConstantModule
{
    [ScriptConstant]
    public static readonly DateTime NOW = DateTime.UnixEpoch;
}
