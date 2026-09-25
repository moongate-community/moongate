using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("static_function")]
public sealed class StaticFunctionModule
{
    [ScriptFunction]
    public static int Twice(int value)
    {
        return value * 2;
    }
}
