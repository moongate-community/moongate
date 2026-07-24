namespace Moongate.Core.Interfaces;

/// <summary>
/// Tells callers whether they are running on the single-writer game-loop thread. Backed by the
/// SquidStd event loop, which owns and reports its own thread.
/// </summary>
public interface ILoopThread
{
    /// <summary>True when the calling thread is the game-loop thread.</summary>
    bool IsOnLoopThread { get; }
}
