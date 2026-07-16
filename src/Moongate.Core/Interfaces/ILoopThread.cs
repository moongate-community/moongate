namespace Moongate.Core.Interfaces;

/// <summary>
/// Tracks the game-loop thread so callers can tell whether they are running on the single-writer
/// thread. <see cref="Capture" /> must be invoked once, on the loop thread, at startup.
/// </summary>
public interface ILoopThread
{
    /// <summary>True when the calling thread is the captured game-loop thread.</summary>
    bool IsOnLoopThread { get; }

    /// <summary>Records the calling thread as the game-loop thread.</summary>
    void Capture();
}
