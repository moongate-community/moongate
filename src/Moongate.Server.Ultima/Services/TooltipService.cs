using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Tooltips;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Templates;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc />
/// <remarks>
///     Clilocs where the client has one, so it shows them in its own language; the server's own texts, such as the
///     rarity, go through <see cref="ILocalizationService" /> as free text (as UOX3). Built on each request: nothing is
///     cached.
/// </remarks>
public sealed class TooltipService : ITooltipService
{
    // The client's item names: 1020000 + graphic, and 1078872 + graphic from 0x4000 (ModernUO's LabelNumber).
    private const int ItemNameCliloc = 1020000;
    private const int HighItemNameCliloc = 1078872;
    private const int HighItemGraphic = 0x4000;
    private const int AmountAndNameCliloc = 1050039; // ~1_NUMBER~ ~2_ITEMNAME~
    private const int BlessedCliloc = 1038021;
    private const int CursedCliloc = 1049643;
    private const int OneStoneCliloc = 1072788; // Weight: ~1_WEIGHT~ stone
    private const int StonesCliloc = 1072789; // Weight: ~1_WEIGHT~ stones
    private const int MobileNameCliloc = 1050045; // ~1_PREFIX~~2_NAME~~3_SUFFIX~

    // messages/*.toml: 30000 + rarity.
    private const int RarityMessageBase = 30000;

    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly ILocalizationService _localization;

    public TooltipService(IItemTemplateService templates, ITileDataService tiles, ILocalizationService localization)
    {
        _templates = templates;
        _tiles = tiles;
        _localization = localization;
    }

    public PropertyList Build(ItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var list = new PropertyList();
        _templates.TryGet(item.TemplateId, out var template);
        AddName(list, item, item.Name ?? template?.Name);

        var lootType = item.TryGetProp<LootType>(ItemPropKeys.LootType, out var own)
                           ? own
                           : template?.EffectiveLootType() ?? LootType.Regular;

        if (lootType is LootType.Blessed or LootType.Newbied)
        {
            list.Add(BlessedCliloc);
        }
        else if (lootType == LootType.Cursed)
        {
            list.Add(CursedCliloc);
        }

        var weight = (int)Math.Round((template?.EffectiveWeight(_tiles) ?? TiledataWeight(item)) * item.Amount);
        list.Add(weight == 1 ? OneStoneCliloc : StonesCliloc, weight.ToString());

        if (item.Rarity != ItemRarityType.Common)
        {
            list.AddText($"<BASEFONT COLOR={RarityColor(item.Rarity)}>{_localization.Get(RarityMessageBase + (int)item.Rarity)}</BASEFONT>");
        }

        return list;
    }

    public PropertyList Build(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        // The client needs a single space for an empty prefix or suffix.
        var list = new PropertyList();
        list.Add(MobileNameCliloc, $" \t{mobile.Name}\t{(string.IsNullOrEmpty(mobile.Title) ? " " : " " + mobile.Title)}");

        return list;
    }

    private static void AddName(PropertyList list, ItemEntity item, string? name)
    {
        if (item.Amount > 1)
        {
            list.Add(AmountAndNameCliloc, $"{item.Amount}\t{name ?? "#" + NameCliloc(item)}");
        }
        else if (name is not null)
        {
            list.AddText(name);
        }
        else
        {
            list.Add(NameCliloc(item));
        }
    }

    private static int NameCliloc(ItemEntity item)
    {
        return item.ItemId < HighItemGraphic ? ItemNameCliloc + item.ItemId : HighItemNameCliloc + item.ItemId;
    }

    private decimal TiledataWeight(ItemEntity item)
    {
        return _tiles.TryGetItem(item.ItemId, out var tile) ? tile.Weight : 0;
    }

    private static string RarityColor(ItemRarityType rarity)
    {
        return rarity switch
        {
            ItemRarityType.Uncommon => "#1EFF00",
            ItemRarityType.Rare => "#0070DD",
            ItemRarityType.Epic => "#A335EE",
            _ => "#FF8000"
        };
    }
}
