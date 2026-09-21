using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
/// One coroutine the scheduler is tracking: its state, the file that owns it, the timer it waits on, and the budget's
/// cancellation source.
/// </summary>
/// <remarks>
/// The source lives as long as the coroutine, not as long as one resume: LuaCSharp 0.5.6 keeps the
/// <see cref="CancellationToken" /> of a coroutine's first <c>ResumeAsync</c> with its suspended frames
/// and checks that one on every later resume, so a per-resume source would go unobserved after the
/// first <c>wait</c>. A cancelled coroutine is dead, so one source per coroutine is the right lifetime.
/// </remarks>
internal sealed class ScheduledCoroutine : IDisposable
{
    public Guid Id { get; }
    public LuaState Coroutine { get; }
    public string Owner { get; }

    /// <summary>
    /// The budget's source for every resume of this coroutine. Cancelled by the instruction hook, and disposed when the
    /// entry leaves the scheduler.
    /// </summary>
    public CancellationTokenSource Budget { get; }

    public string? PendingTimer { get; set; }

    public ScheduledCoroutine(Guid id, LuaState coroutine, string owner)
    {
        Id = id;
        Coroutine = coroutine;
        Owner = owner;
        Budget = new();
    }

    public void Dispose()
        => Budget.Dispose();
}
