using Moongate.Scripting.Interfaces;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     Records how often, and for which member, the binder asked for the thread check.
/// </summary>
public sealed class CountingThreadGuard : IScriptThreadGuard
{
    public int Calls { get; private set; }
    public string? LastMember { get; private set; }

    public void EnsureScriptThread(string member)
    {
        Calls++;
        LastMember = member;
    }
}
