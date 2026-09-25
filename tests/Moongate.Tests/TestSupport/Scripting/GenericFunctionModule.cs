using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("generic_function")]
public sealed class GenericFunctionModule
{
    [ScriptFunction]
    public int Count<T>()
    {
        return typeof(T).GetHashCode();
    }
}
