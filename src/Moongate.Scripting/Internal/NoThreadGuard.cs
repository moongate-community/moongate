using Moongate.Scripting.Interfaces;

namespace Moongate.Scripting.Internal;

/// <summary>For hosts without a game loop: tests, tools, and the package README example.</summary>
internal sealed class NoThreadGuard : IScriptThreadGuard
{
    public static NoThreadGuard Instance { get; } = new();

    private NoThreadGuard()
    {
    }

    public void EnsureScriptThread(string member)
    {
    }
}
