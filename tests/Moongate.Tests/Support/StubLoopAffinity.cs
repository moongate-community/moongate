using Moongate.Core.Interfaces;

namespace Moongate.Tests.Support;

/// <summary>Test double for <see cref="ILoopAffinity" /> that never trips — the loop is assumed present.</summary>
public sealed class StubLoopAffinity : ILoopAffinity
{
    public void AssertOnLoop(string operation)
    {
    }
}
