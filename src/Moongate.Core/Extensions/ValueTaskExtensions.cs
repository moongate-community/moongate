namespace Moongate.Core.Extensions;

/// <summary>
/// Helpers for consuming <see cref="ValueTask" /> results from synchronous code paths (such as the
/// network-thread packet handlers, which cannot be <c>async</c>).
/// </summary>
public static class ValueTaskExtensions
{
    /// <summary>
    /// Blocks until <paramref name="task" /> completes and observes any exception. Consumes the result
    /// only after confirming completion, satisfying CA2012 (a <see cref="ValueTask" /> must not have its
    /// result read before it has completed).
    /// </summary>
    public static void WaitSync(this ValueTask task)
    {
        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
        }
        else
        {
            task.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Blocks until <paramref name="task" /> completes and returns its result, consuming it only after
    /// confirming completion (CA2012-safe). Overload of <see cref="WaitSync(ValueTask)" /> for value-returning
    /// value tasks such as <c>RemoveAsync</c>.
    /// </summary>
    public static T WaitSync<T>(this ValueTask<T> task)
        => task.IsCompleted ? task.GetAwaiter().GetResult() : task.AsTask().GetAwaiter().GetResult();
}
