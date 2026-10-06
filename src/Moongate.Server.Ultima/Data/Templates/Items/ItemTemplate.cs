using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Server.Ultima.Utils;
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
    // The numbers of the combat fields are kept within what a client and the formulas can take.
    private const int MaximumCombatNumber = 65535;
    private const int MaximumSpeed = 500;
    private const int MaximumArmorRating = 500;

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
    ///         ITileDataService
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
    ///     The global Lua table, defined by <c>scripts/items/&lt;script_id&gt;.lua</c>, whose functions handle the item's
    ///     events, such as <c>on_use</c>. A lower-case Lua identifier; empty: no script.
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
    ///     Whether the item is a weapon held in both hands (a bow, a halberd), as POL's itemdesc <c>TwoHanded</c>: worn
    ///     on <see cref="LayerType.TwoHanded" />, it leaves no hand free. Anything else on that layer (a shield, a
    ///     torch) is held in the other hand and goes with a one-handed weapon.
    /// </summary>
    public bool? TwoHandedWeapon { get; set; }

    /// <summary>
    ///     The skill a weapon is fought with, as UOX3 types it by graphic: swordsmanship, mace fighting, fencing, archery
    ///     or throwing. Unset for what is not a weapon.
    /// </summary>
    public SkillType? WeaponSkill { get; set; }

    /// <summary>
    ///     The least damage a hit of the weapon does, before the bonuses of the one who wields it.
    /// </summary>
    public int? DamageMin { get; set; }

    /// <summary>
    ///     The most damage a hit of the weapon does. A worn item with one above 0 is a weapon.
    /// </summary>
    public int? DamageMax { get; set; }

    /// <summary>
    ///     The speed of a weapon, ModernUO's: a swing takes 15000 / ((stamina + 100) * speed) seconds.
    /// </summary>
    public int? Speed { get; set; }

    /// <summary>
    ///     The strength a wearer needs for the item, as UOX3's <c>str</c>; read, not enforced yet.
    /// </summary>
    public int? StrengthRequired { get; set; }

    /// <summary>
    ///     The armor rating of a piece of armor or a shield, as UOX3's <c>def</c>.
    /// </summary>
    public int? ArmorRating { get; set; }

    /// <summary>
    ///     The most hit points the item has, its durability; read, not used yet.
    /// </summary>
    public int? MaxHits { get; set; }

    /// <summary>
    ///     Whether a dye tub can give the item its hue, as UOX3's <c>dyeable</c>: clothing is, a death robe is not.
    ///     Unset is not dyeable.
    /// </summary>
    public bool? Dyeable { get; set; }

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
    ///     Free values for scripts, such as a quest step. A child template's tags, when set, replace its base's; they are not merged.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    ///     The lowest account type that sees the item, such as <see cref="AccountType.GameMaster" /> for spawners,
    ///     which players never see. Null, the default, is unset: the template inherits it through
    ///     <see cref="BaseId" />, and an item with none anywhere is visible to everyone.
    /// </summary>
    public AccountType? Visibility { get; set; }

    /// <summary>
    ///     The hue to apply over the graphic's own art, or a range to pick a fresh one from on every spawn. 0 means the
    ///     art's native coloring. Unset takes the base template's hue, else 0.
    /// </summary>
    public HueSpec? Hue { get; set; }

    /// <summary>
    ///     Maximum item count for a container template; null for anything that is not a container.
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    ///     Maximum carried weight for a container template; null for anything that is not a container.
    /// </summary>
    public int? MaxWeight { get; set; }

    /// <summary>
    ///     Loot template ids, each rolled once into the container when a spawn makes it; list one twice to roll it
    ///     twice. Unset takes the base template's, else none.
    /// </summary>
    public List<string>? Loot { get; set; }

    /// <summary>
    ///     Gold in the container when a spawn makes it. Unset takes the base template's, else 0.
    /// </summary>
    public DiceSpec? Gold { get; set; }

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

        if (Gold is { Min: < 0 })
        {
            throw Invalid("gold", "must not roll below 0");
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

        if (DamageMin is < 0 or > MaximumCombatNumber)
        {
            throw Invalid("damage_min", $"must be from 0 to {MaximumCombatNumber}");
        }

        if (DamageMax is < 0 or > MaximumCombatNumber || DamageMin > DamageMax)
        {
            throw Invalid("damage_max", $"must be from damage_min to {MaximumCombatNumber}");
        }

        if (Speed is < 1 or > MaximumSpeed)
        {
            throw Invalid("speed", $"must be from 1 to {MaximumSpeed}");
        }

        if (ArmorRating is < 0 or > MaximumArmorRating)
        {
            throw Invalid("armor_rating", $"must be from 0 to {MaximumArmorRating}");
        }

        if (StrengthRequired is < 0 or > MaximumCombatNumber)
        {
            throw Invalid("strength_required", $"must be from 0 to {MaximumCombatNumber}");
        }

        if (MaxHits is < 0 or > MaximumCombatNumber)
        {
            throw Invalid("max_hits", $"must be from 0 to {MaximumCombatNumber}");
        }

        if (Tags is not null && Tags.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw Invalid("tags", "must not have an empty key");
        }

        if (!string.IsNullOrEmpty(ScriptId) && !ScriptIdUtils.IsValid(ScriptId))
        {
            throw Invalid("script_id", ScriptIdUtils.Rule);
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
