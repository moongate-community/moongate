using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Templates;

namespace Moongate.Server.Ultima.Data.Templates.Items;

/// <summary>
///     An authored item definition, one TOML entry under
///     <c>
///         templates/items/
///     </c>
///     .
/// </summary>
public class ItemTemplate
{
    /// <summary>
    ///     The stable id a loot table, a spawn or the
    ///     <c>
    ///         additem
    ///     </c>
    ///     command names this template by.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     The <see cref="Id" /> of another <see cref="ItemTemplate" /> this one inherits unset fields
    ///     from, the way UOX3's
    ///     <c>
    ///         get=
    ///     </c>
    ///     chains one door variant off another. Resolved by the loader
    ///     across every loaded file, not by this type itself.
    /// </summary>
    public string? BaseId { get; set; }

    /// <summary>
    ///     The base client graphic. Physical properties tiledata already carries, weight, layer,
    ///     stackability, are read from it through
    ///     <c>
    ///         IItemCatalog
    ///     </c>
    ///     at the point of use, not restated here.
    /// </summary>
    public Serial ItemId { get; set; }

    /// <summary>
    ///     Overrides tiledata's own name, which is often generic ("a book"), when set.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     A designer's note. Read by nobody at runtime.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    ///     A fixed rarity, or a policy for picking one at random on every spawn.
    /// </summary>
    public EnumValueSpec<ItemRarityType> Rarity { get; set; } =
        EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Common);

    /// <summary>
    ///     Names a Lua module handling this template's behaviour. The engine calls whichever of its
    ///     well-known functions exists,
    ///     <c>
    ///         on_use
    ///     </c>
    ///     ,
    ///     <c>
    ///         on_equip
    ///     </c>
    ///     ,
    ///     <c>
    ///         on_unequip
    ///     </c>
    ///     and so on; a
    ///     template with nothing to react to leaves this unset.
    /// </summary>
    public string ScriptId { get; set; }

    /// <summary>
    ///     Whether the item can be picked up. Not derivable from tiledata, which carries no such flag;
    ///     POL and UOX3 both need the same explicit field for the same reason.
    /// </summary>
    public bool Movable { get; set; } = true;

    /// <summary>
    ///     The lowest account type that sees the item, such as <see cref="AccountType.GameMaster" /> for spawners,
    ///     which players never see. Null, the default, is unset: the template inherits it through
    ///     <see cref="BaseId" />, and an item with none anywhere is visible to everyone.
    /// </summary>
    public AccountType? Visibility { get; set; }

    /// <summary>
    ///     The hue to apply over the graphic's own art, or a range to pick a fresh one from on every spawn.
    ///     0, the default, means the art's native coloring: no override.
    /// </summary>
    public HueSpec Hue { get; set; } = HueSpec.FromValue(0);

    /// <summary>
    ///     Maximum item count for a container template; null for anything that is not a container.
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    ///     Maximum carried weight for a container template; null for anything that is not a container.
    /// </summary>
    public int? MaxWeight { get; set; }

    /// <summary>
    ///     Gets whether an account of type <paramref name="viewer" /> sees items made from this template: it must be at
    ///     least <see cref="Visibility" />, and everyone sees an item without one.
    /// </summary>
    public bool IsVisibleTo(AccountType viewer)
    {
        return viewer >= (Visibility ?? AccountType.Regular);
    }
}
