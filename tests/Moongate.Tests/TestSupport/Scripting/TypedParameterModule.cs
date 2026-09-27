using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A module whose parameter declares its Lua type for the definitions.
/// </summary>
[ScriptModule("typed")]
public sealed class TypedParameterModule
{
    [ScriptFunction]
    public bool Accept([ScriptParameterType("EventName")] string name)
    {
        return name.Length > 0;
    }
}
