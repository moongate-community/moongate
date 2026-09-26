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
    /// <summary>
    ///     Computes a block's Id and item Serial, with no dependency on any other block. Used both to
    ///     precompute the full header-to-Id map up front and, once that map exists, by <see cref="Build" />.
    ///     False for a block with no id= of its own (a get=a b alias, or a non-item block).
    /// </summary>
    public static bool TryComputeId(DfnBlock block, out string id, out Serial itemId)
    {
        if (!block.Fields.TryGetValue("id", out var idText) || !Serial.TryParse(idText, out itemId))
        {
            id = "";
            itemId = default;

            return false;
        }

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

    public static ItemTemplate? Build(DfnBlock block, IReadOnlyDictionary<string, string> idByHeader)
    {
        // The Id is the one precomputed from the block's own lines, not from inlined get= lines.
        if (!TryComputeId(block, out _, out var itemId) || !idByHeader.TryGetValue(block.Header, out var id))
        {
            return null;
        }

        var template = new ItemTemplate
        {
            Id = id,
            ItemId = itemId,
            Movable = ReadMovable(block)
        };

        ApplyBaseFields(block, template);

        // UOX3's visible= is 0 for everyone; 1 (hidden), 2 (magically invisible) and 3 (GM hidden) all keep the
        // item from players, the closest being visible to staff only.
        if (block.Fields.TryGetValue("visible", out var visibleText) && int.TryParse(visibleText, out var visible) &&
            visible is >= 1 and <= 3)
        {
            template.Visibility = AccountType.GameMaster;
        }

        if (block.Fields.TryGetValue("name", out var displayName) && displayName.Length > 0)
        {
            template.Name = displayName;
        }

        if (block.Fields.TryGetValue("color", out var colorText) && HueSpec.TryParse(colorText, out var hue))
        {
            template.Hue = hue;
        }

        if (block.Fields.TryGetValue("weightmax", out var weightMaxText) && int.TryParse(weightMaxText, out var weightMax))
        {
            template.MaxWeight = weightMax;
        }

        if (block.Fields.TryGetValue("get", out var getText))
        {
            var targets = getText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Only single-parent inheritance maps onto BaseId. get=a b names an alias, not a parent;
            // an unresolved single target (its own block had no id=, or was never converted) is
            // dropped the same as any other field this converter cannot carry over faithfully.
            if (targets.Length == 1 && idByHeader.TryGetValue(targets[0], out var baseId))
            {
                template.BaseId = baseId;
            }
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
        if (block.Fields.TryGetValue("weight", out var weightText) && int.TryParse(weightText, out var hundredths))
        {
            template.Weight = hundredths / 100m;
        }

        if (block.Fields.TryGetValue("amount", out var amountText) && int.TryParse(amountText, out var amount) && amount >= 1)
        {
            template.Amount = RangeValueSpec<int>.FromValue(amount);
        }

        if (block.Fields.TryGetValue("pileable", out var pileable))
        {
            template.Stackable = pileable == "1";
        }

        if (block.Fields.TryGetValue("layer", out var layerText) && byte.TryParse(layerText, out var layer) &&
            Enum.IsDefined((LayerType)layer) && layer != 0)
        {
            template.Layer = (LayerType)layer;
        }

        // value=buy sell; one number sets both.
        if (block.Fields.TryGetValue("value", out var valueText))
        {
            var prices = valueText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (prices.Length >= 1 && int.TryParse(prices[0], out var buy))
            {
                template.BuyPrice = buy;
                template.SellPrice = prices.Length >= 2 && int.TryParse(prices[1], out var sell) ? sell : buy;
            }
        }

        if (block.Fields.TryGetValue("decay", out var decay))
        {
            template.Decays = decay == "1";
        }

        // newbie is usually a bare flag line, sometimes newbie=1.
        if (block.Entries.Any(line => line.Trim().Equals("newbie", StringComparison.OrdinalIgnoreCase)) ||
            block.Fields.TryGetValue("newbie", out var newbie) && newbie == "1")
        {
            template.LootType = LootType.Newbied;
        }

        ApplyTags(block, template);
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
