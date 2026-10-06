using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Questions put to an item's script before a move, such as <c>can_pick_up</c>.
/// </summary>
public static class ItemScriptServiceExtensions
{
    /// <summary>
    ///     Asks the item's script whether a move may follow. Only a function that returns <c>false</c> refuses it: no
    ///     script service, no script, a missing function, an error, a <c>wait</c> or any other value let it follow.
    /// </summary>
    public static bool Allows(this IItemScriptService? scripts, ItemEntity item, string function, params object?[] args)
    {
        return scripts is null || scripts.Run(item, function, args) is not
            { Kind: ScriptResultKind.Completed, Values: [false, ..] };
    }
}
