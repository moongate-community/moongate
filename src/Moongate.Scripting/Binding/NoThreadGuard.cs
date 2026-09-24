using Moongate.Scripting.Interfaces;

namespace Moongate.Scripting.Binding;

/// <summary>For hosts without a game loop: tests, tools, and the package README example.</summary>
public sealed class NoThreadGuard : IScriptThreadGuard
{
    /// <summary>Gets the single shared instance of the guard.</summary>
    public static NoThreadGuard Instance { get; } = new();

    private NoThreadGuard() { }

    /// <summary>Does nothing: this guard never rejects a caller.</summary>
    /// <param name="member">The name of the member being called, ignored.</param>
    public void EnsureScriptThread(string member) { }
}
