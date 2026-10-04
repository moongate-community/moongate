using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Builds an <see cref="ItemTemplate" /> from one parsed block, resolving its
///     <c>
///         get=
///     </c>
///     target against a fully precomputed header-to-Id map.
/// </summary>
internal static class ItemTemplateBuilder
{
    // UOX3's item type of food (IT_FOOD).
    private const int FoodType = 14;

    // UOX3's item type of drinks (IT_DRINK).
    private const int DrinkType = 105;

    // What UOX3 files under drinks and nobody drinks: an ingredient.
    private static readonly HashSet<string> NotDrunk = new(StringComparer.Ordinal) { "0x09ec_jar_of_honey" };

    // What UOX3 files under food and nobody eats as it is: an ingredient, and the fish that ModernUO gives a spell.
    private static readonly HashSet<string> NotEaten = new(StringComparer.Ordinal)
    {
        "0x0a1e_bowl_of_flour",
        "base_magic_fish"
    };

    // UOX3's IT_SHIELD item type.
    private const int UoxShieldType = 107;

    /// <summary>
    ///     Computes a block's Id and item Serial, with no dependency on any other block. Used both to
    ///     precompute the full header-to-Id map up front and, once that map exists, by <see cref="Build" />.
    ///     False for a block with no id= of its own (a get=a b alias, or a non-item block).
    /// </summary>
    public static bool TryComputeId(DfnBlock block, out string id, out Serial itemId)
    {
        // UOX3 picks one id of a list (id=0x0c4f 0x0c50) at random; a template has one graphic, so the first is kept.
        if (!block.Fields.TryGetValue("id", out var idText) || !UoxNumber.TryParse(idText, out var graphic) || graphic < 0)
        {
            id = "";
            itemId = default;

            return false;
        }

        itemId = new Serial((uint)graphic);

        // The header alone is always unique (duplicates are caught and warned about while every
        // block is being read). name= is not: UOX3 reuses it across many facing, material or
        // damage-state variants of the same conceptual thing, sometimes literally "#", so it can
        // only ever be an addition to the header, never a replacement for it. name= is free text
        // ("bone gloves", "smith's hammer"), so the combined Id goes through ToSnakeCase, the same
        // normalization EnumValueSpec already writes its own text form through.
        id = StringUtils.ToSnakeCase(
            IsBareHex(block.Header) && block.Fields.TryGetValue("name", out var name) && name.Length > 0
                ? $"{block.Header}_{name}"
                : block.Header
        );

        return true;
    }

    /// <summary>
    ///     Gets the one parent a block inherits from (<see cref="DfnBlockExtensions.ParentTargets" />); false for none
    ///     or for a random <c>get=a b</c>.
    /// </summary>
    public static bool TryGetSingleParent(DfnBlock block, out string parent)
    {
        var targets = block.ParentTargets();
        parent = targets.Length == 1 ? targets[0] : "";

        return targets.Length == 1;
    }

    public static ItemTemplate? Build(
        DfnBlock block,
        IReadOnlyDictionary<string, string> idByHeader,
        UoxScriptAssociations? scripts = null
    )
    {
        // The Id is the one precomputed up front. A block with no id= of its own keeps item_id 0, which the server's
        // loader fills from its base_id.
        if (!idByHeader.TryGetValue(block.Header, out var id))
        {
            return null;
        }

        TryComputeId(block, out _, out var itemId);

        var template = new ItemTemplate
        {
            Id = id,
            ItemId = itemId,
            Movable = ReadMovable(block)
        };

        ApplyBaseFields(block, template);

        if (scripts?.ScriptIdFor(block, (int)itemId.Value) is { } scriptId)
        {
            template.ScriptId = scriptId;
        }
        else if (!NotEaten.Contains(id) &&
                 block.Fields.TryGetValue("TYPE", out var type) &&
                 UoxNumber.TryParse(type, out var kind) &&
                 kind == FoodType)
        {
            // What UOX3 lets a player eat: scripts/items/food.lua.
            template.ScriptId = "food";
        }
        else if (!NotDrunk.Contains(id) &&
                 block.Fields.TryGetValue("TYPE", out var drinkType) &&
                 UoxNumber.TryParse(drinkType, out var drinkKind) &&
                 drinkKind == DrinkType)
        {
            // What UOX3 lets a player drink: scripts/items/drink.lua, in place of UOX3's own pitchers.js.
            template.ScriptId = "drink";
        }

        // UOX3's visible= is 0 for everyone; 1 (hidden), 2 (magically invisible) and 3 (GM hidden) all keep the
        // item from players, the closest being visible to staff only.
        if (block.Fields.TryGetValue("visible", out var visibleText) && UoxNumber.TryParse(visibleText, out var visible))
        {
            // 0 is everyone, written out so it overrides a hidden parent such as base_spawner.
            template.Visibility = visible is >= 1 and <= 3 ? AccountType.GameMaster : AccountType.Regular;
        }

        if (block.Fields.TryGetValue("name", out var displayName) && displayName.Length > 0)
        {
            template.Name = displayName;
        }

        // UOX3 reads COLOR and COLOUR as one tag.
        if ((block.Fields.TryGetValue("color", out var colorText) || block.Fields.TryGetValue("colour", out colorText)) &&
            HueSpec.TryParse(colorText, out var hue))
        {
            template.Hue = hue;
        }

        if (block.Fields.TryGetValue("weightmax", out var weightMaxText) && UoxNumber.TryParse(weightMaxText, out var weightMax))
        {
            template.MaxWeight = weightMax;
        }

        // Only single-parent inheritance maps onto BaseId. get=a b names an alias, not a parent; an unresolved single
        // target (never converted) is dropped the same as any other field this converter cannot carry over
        // faithfully. A block that gets itself (UOX3 data has [0x27c2] with get=0x27c2) inherits nothing.
        if (TryGetSingleParent(block, out var parent) && idByHeader.TryGetValue(parent, out var baseId) && baseId != id)
        {
            template.BaseId = baseId;
        }

        return template;
    }

    private static bool IsBareHex(string header)
    {
        return header.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
    }

    // UOX3 movable= is 0 for the client default, 1 always, 2 never and 3 owner only; the default stays unset so the
    // template follows tiledata.
    private static bool? ReadMovable(DfnBlock block)
    {
        if (!block.Fields.TryGetValue("movable", out var text))
        {
            return null;
        }

        return text switch
        {
            "1" or "3" => true,
            "2" => false,
            _ => null
        };
    }

    private static void ApplyBaseFields(DfnBlock block, ItemTemplate template)
    {
        // UOX3 weighs in hundredths of a stone: weight=700 is 7 stones, a coin's weight=2 is 0.02.
        if (block.Fields.TryGetValue("weight", out var weightText) && UoxNumber.TryParse(weightText, out var hundredths))
        {
            template.Weight = hundredths / 100m;
        }

        if (block.Fields.TryGetValue("amount", out var amountText) && UoxNumber.TryParse(amountText, out var amount) && amount >= 1)
        {
            template.Amount = RangeValueSpec<int>.FromValue(amount);
        }

        if (block.Fields.TryGetValue("pileable", out var pileableText) && UoxNumber.TryParse(pileableText, out var pileable))
        {
            template.Stackable = pileable != 0;
        }

        if (block.Fields.TryGetValue("layer", out var layerText) && UoxNumber.TryParse(layerText, out var layer) &&
            layer is > 0 and <= byte.MaxValue && Enum.IsDefined((LayerType)layer))
        {
            template.Layer = (LayerType)layer;
        }

        // As UOX3 decides at equip time: layer 2 takes both hands unless the item is a shield (type=107) or a light
        // (dir=, a torch or lantern), which go in the other hand.
        if (template.Layer == LayerType.TwoHanded && !IsShield(block) && !IsLight(block))
        {
            template.TwoHandedWeapon = true;
        }

        // value=buy sell; one number sets both.
        if (block.Fields.TryGetValue("value", out var valueText))
        {
            var prices = valueText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (prices.Length >= 1 && UoxNumber.TryParse(prices[0], out var buy))
            {
                template.BuyPrice = buy;
                template.SellPrice = prices.Length >= 2 && UoxNumber.TryParse(prices[1], out var sell) ? sell : buy;
            }
        }

        if (block.Fields.TryGetValue("decay", out var decayText) && UoxNumber.TryParse(decayText, out var decay))
        {
            template.Decays = decay != 0;
        }

        // newbie is usually a bare flag line, sometimes newbie=1.
        if (block.Entries.Any(line => line.Trim().Equals("newbie", StringComparison.OrdinalIgnoreCase)) ||
            block.Fields.TryGetValue("newbie", out var newbie) && newbie == "1")
        {
            template.LootType = LootType.Newbied;
        }

        ApplyTags(block, template);
    }

    private static bool IsShield(DfnBlock block)
    {
        return block.Fields.TryGetValue("type", out var typeText) && UoxNumber.TryParse(typeText, out var type) && type == UoxShieldType;
    }

    private static bool IsLight(DfnBlock block)
    {
        return block.Fields.TryGetValue("dir", out var dirText) && UoxNumber.TryParse(dirText, out var dir) && dir != 0;
    }

    // custominttag=name value and customstringtag=name text can repeat, so they are read from every line.
    private static void ApplyTags(DfnBlock block, ItemTemplate template)
    {
        foreach (var line in block.Entries)
        {
            var separator = line.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();

            if (!key.Equals("custominttag", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("customstringtag", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line[(separator + 1)..].Trim().Split(' ', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 2 && parts[0].Length > 0)
            {
                template.Tags ??= new();
                template.Tags[parts[0]] = parts[1];
            }
        }
    }
}
