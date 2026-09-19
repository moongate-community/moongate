using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>A module whose only function throws, to prove exceptions become Lua errors.</summary>
[ScriptModule("thrower")]
public sealed class ThrowingModule
{
    [ScriptFunction]
    public int Fail()
    {
        throw new InvalidOperationException("deliberate");
    }
}
