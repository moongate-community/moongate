using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>One coroutine the scheduler is tracking: its state, the file that owns it, and the timer it waits on.</summary>
internal sealed class ScheduledCoroutine
{
    public Guid Id { get; }
    public LuaState Coroutine { get; }
    public string Owner { get; }
    public string? PendingTimer { get; set; }

    public ScheduledCoroutine(Guid id, LuaState coroutine, string owner)
    {
        Id = id;
        Coroutine = coroutine;
        Owner = owner;
    }
}
