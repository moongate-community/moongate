using Moongate.Server.Data.Items;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Dispatches item lifecycle hooks to Lua script callbacks.
/// </summary>
public interface IItemScriptDispatcher
{
    /// <summary>
    /// Returns <c>true</c> when at least one Lua hook is resolvable for the item/hook pair.
    /// </summary>
    /// <param name="item">Item to resolve.</param>
    /// <param name="hook">Hook name (e.g. <c>single_click</c>, <c>double_click</c>).</param>
    bool HasHook(UOItemEntity item, string hook);

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
