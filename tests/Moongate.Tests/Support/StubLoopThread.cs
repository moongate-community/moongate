using Moongate.Core.Interfaces;

namespace Moongate.Tests.Support;

/// <summary>Test double for <see cref="ILoopThread" /> with a fixed on-loop answer.</summary>
public sealed class StubLoopThread : ILoopThread
{
    public StubLoopThread(bool onLoop = true)
    {
        IsOnLoopThread = onLoop;
    }

    public bool IsOnLoopThread { get; }
}
