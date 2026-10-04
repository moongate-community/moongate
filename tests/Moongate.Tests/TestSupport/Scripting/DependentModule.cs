using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A module that cannot be built without a service, for describing a module from its type alone.
/// </summary>
[ScriptModule("dependent", "Needs a service to exist.")]
public sealed class DependentModule
{
    private readonly IScriptThreadGuard _guard;

    public DependentModule(IScriptThreadGuard guard)
    {
        _guard = guard;
    }

    [ScriptFunction(helpText: "Asks.")]
    public bool AskTwice(string member)
    {
        _guard.EnsureScriptThread(member);

        return true;
    }
}
