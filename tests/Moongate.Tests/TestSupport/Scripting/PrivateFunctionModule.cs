using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("private_function")]
public sealed class PrivateFunctionModule
{
    [ScriptFunction]
    private int Hidden()
    {
        return 1;
    }
}
