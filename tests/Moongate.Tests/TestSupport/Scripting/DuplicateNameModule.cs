using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("twice")]
public sealed class DuplicateNameModule
{
    [ScriptConstant("same")]
    public static readonly int A = 1;

    [ScriptFunction("same")]
    public int B()
    {
        return 2;
    }
}
