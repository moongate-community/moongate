using Moongate.Server.Data.Items;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Dispatches item lifecycle hooks to Lua script callbacks.
/// </summary>
public interface IItemScriptDispatcher
{
    /// <summary>
    /// Dispatches a hook for the provided item script context.
    /// </summary>
    /// <param name="context">Dispatch context with item, hook and optional metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> when a valid dispatch was attempted; otherwise <c>false</c>.
    /// </returns>
    Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default);
}
