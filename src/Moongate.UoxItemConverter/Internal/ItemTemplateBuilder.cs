using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
/// Builds an <see cref="ItemTemplate" /> from one parsed block, resolving its <c>get=</c>
/// target against a fully precomputed header-to-Id map.
/// </summary>
internal static class ItemTemplateBuilder
{
    /// <summary>
    /// Computes a block's Id and item Serial, with no dependency on any other block. Used both to
    /// precompute the full header-to-Id map up front and, once that map exists, by <see cref="Build" />.
    /// False for a block with no id= of its own (a get=a b alias, or a non-item block).
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
        if (!TryComputeId(block, out var id, out var itemId))
        {
            return null;
        }

        var template = new ItemTemplate
        {
            Id = id,
            ItemId = itemId,
            Movable = block.Fields.TryGetValue("movable", out var movable) && movable == "1"
        };

        if (block.Fields.TryGetValue("name", out var displayName) && displayName.Length > 0)
        {
            template.Name = displayName;
        }

        if (block.Fields.TryGetValue("color", out var colorText) && Serial.TryParse(colorText, out var color))
        {
            template.Hue = RangeValueSpec<int>.FromValue((int)color.Value);
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
        => header.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
}
