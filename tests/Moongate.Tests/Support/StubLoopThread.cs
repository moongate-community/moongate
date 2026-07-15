using Moongate.Core.Interfaces;

namespace Moongate.Tests.Support;

/// <summary>Test double for <see cref="ILoopThread" /> with a fixed on-loop answer.</summary>
public sealed class StubLoopThread : ILoopThread
{
    private readonly bool _onLoop;

    public StubLoopThread(bool onLoop = true)
    {
        _onLoop = onLoop;
    }

    public bool IsOnLoopThread => _onLoop;

    public void Capture()
    {
    }
}
