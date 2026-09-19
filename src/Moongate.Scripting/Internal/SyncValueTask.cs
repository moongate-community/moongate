using System.Runtime.ExceptionServices;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Drives LuaCSharp's ValueTask-shaped API synchronously. Nothing a module exposes is genuinely
/// asynchronous, so a task that is still pending when the call returns is a bug, not something to await.
/// </summary>
internal static class SyncValueTask
{
    public static T Run<T>(ValueTask<T> task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException(
                "Lua execution went asynchronous. Script code must complete on the calling thread.");
        }

        if (task.IsFaulted)
        {
            var wrapped = task.AsTask().Exception!;
            ExceptionDispatchInfo.Capture(wrapped.InnerExceptions.Count == 1 ? wrapped.InnerException! : wrapped).Throw();
        }

        return task.Result;
    }

    public static void Run(ValueTask task)
    {
        if (!task.IsCompleted)
        {
            throw new InvalidOperationException(
                "Lua execution went asynchronous. Script code must complete on the calling thread.");
        }

        task.GetAwaiter().GetResult();
    }
}
