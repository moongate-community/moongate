namespace Moongate.Scripting.Internal;

/// <summary>
///     Drives LuaCSharp's ValueTask-shaped API synchronously. Nothing a module exposes is genuinely
///     asynchronous, so a task that is still pending when the call returns is a bug, not something to await.
/// </summary>
internal static class SyncValueTask
{
    public static T Run<T>(ValueTask<T> task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException(
                "Lua execution went asynchronous. Script code must complete on the calling thread."
            );
        }

        // ValueTask<T>.Result goes through GetAwaiter().GetResult(), which rethrows the original
        // exception unwrapped; there is nothing to unwrap by hand.
        return task.Result;
    }

    public static void Run(ValueTask task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException(
                "Lua execution went asynchronous. Script code must complete on the calling thread."
            );
        }

        task.GetAwaiter().GetResult();
    }
}
