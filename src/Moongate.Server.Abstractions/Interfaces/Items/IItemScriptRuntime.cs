using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Types.Items;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>
/// Runs the Lua hooks an item's <c>ScriptId</c> points at. Resolution is by id alone: an item with
/// <c>ScriptId: magic_torch</c> runs <c>&lt;scripts&gt;/items/magic_torch.lua</c>, which returns a
/// table whose <c>on_*</c> functions are the hooks.
/// </summary>
public interface IItemScriptRuntime
{
    /// <summary>
    /// True when the script named <paramref name="scriptId" /> exists and defines
    /// <paramref name="hook" />. Answers without running anything.
    /// </summary>
    bool HasHook(string scriptId, ItemScriptHookType hook);

    /// <summary>
    /// Runs <paramref name="hook" /> for the item in <paramref name="context" />. Returns false when
    /// the item has no script, the script has no such hook, or the call failed — a script error is
    /// logged and swallowed, never propagated into the game loop.
    /// </summary>
    bool Invoke(ItemScriptHookType hook, ItemScriptContext context);
}
