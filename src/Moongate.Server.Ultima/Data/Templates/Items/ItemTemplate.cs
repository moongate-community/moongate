using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

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
    ///     Whether the item can be picked up. Unset uses tiledata: movable unless its weight is 255, the client's
    ///     "cannot be lifted".
    /// </summary>
    public bool? Movable { get; set; }

    /// <summary>
    ///     The weight in stones, to at most two decimals: a gold coin is 0.02. Unset uses the tiledata weight, which
    ///     holds whole stones only.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    ///     The stack size of a new item, fixed or a range picked on every spawn. Unset is 1.
    /// </summary>
    public RangeValueSpec<int>? Amount { get; set; }

    /// <summary>
    ///     Whether items stack. Unset uses the tiledata <see cref="TileFlagType.Generic" /> flag.
    /// </summary>
    public bool? Stackable { get; set; }

    /// <summary>
    ///     The layer the item is worn on. Unset uses the tiledata layer of <see cref="ItemId" />.
    /// </summary>
    public LayerType? Layer { get; set; }

    /// <summary>
    ///     The price vendors sell the item for; unset means vendors do not sell it.
    /// </summary>
    public int? BuyPrice { get; set; }

    /// <summary>
    ///     The price vendors pay for the item; unset means vendors do not buy it.
    /// </summary>
    public int? SellPrice { get; set; }

    /// <summary>
    ///     Whether the item decays when left on the ground. Unset decays when the item is movable.
    /// </summary>
    public bool? Decays { get; set; }

    /// <summary>
    ///     Minutes before a decaying item disappears. Unset is 60, ModernUO's default.
    /// </summary>
    public int? DecayMinutes { get; set; }

    /// <summary>
    ///     What happens to the item when its owner dies. Unset is <see cref="Types.Templates.LootType.Regular" />.
    /// </summary>
    public LootType? LootType { get; set; }

    /// <summary>
    ///     Free values for scripts, such as a quest step. A child template's tags add to and override its base's.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

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
    ///     Checks the values a template author can get wrong; the template loader calls it for every template.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     A value is out of range; the message names the template and the field.
    /// </exception>
    public void Validate()
    {
        if (Weight is { } weight && (weight < 0 || decimal.Round(weight, 2) != weight))
        {
            throw Invalid("weight", "must be 0 or more with at most two decimals");
        }

        if (Amount is { } amount && amount.Min < 1)
        {
            throw Invalid("amount", "must be at least 1");
        }

        if (BuyPrice < 0)
        {
            throw Invalid("buy_price", "must be 0 or more");
        }

        if (SellPrice < 0)
        {
            throw Invalid("sell_price", "must be 0 or more");
        }

        if (DecayMinutes < 1)
        {
            throw Invalid("decay_minutes", "must be at least 1");
        }

        if (Tags is not null && Tags.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw Invalid("tags", "must not have an empty key");
        }
    }

    /// <summary>
    ///     Gets whether an account of type <paramref name="viewer" /> sees items made from this template: it must be at
    ///     least <see cref="Visibility" />, and everyone sees an item without one.
    /// </summary>
    public bool IsVisibleTo(AccountType viewer)
    {
        return viewer >= (Visibility ?? AccountType.Regular);
    }

    private InvalidDataException Invalid(string field, string rule)
    {
        return new($"Item template '{Id}': {field} {rule}.");
    }
}
