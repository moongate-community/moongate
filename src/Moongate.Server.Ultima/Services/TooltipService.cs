using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Tooltips;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Tooltips;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc />
/// <remarks>
///     The texts are the server's, in the server language, through <see cref="ILocalizationService" /> as free text
///     (as UOX3): blessed and cursed, the weight and the rarity. Names stay the client's clilocs (its item names, the
///     amount and a mobile's name and title) until the server has translated names. A tooltip depends only on a few
///     fields of its item or mobile (<see cref="ItemTooltipKey" />, <see cref="MobileTooltipKey" />), so it is cached by
///     them: a change gives another key, and nothing is ever invalidated. The returned lists are shared and must not
///     be changed.
/// </remarks>
public sealed class TooltipService : ITooltipService
{
    /// <summary>
    ///     How many distinct tooltips are kept; a full cache starts over.
    /// </summary>
    public const int MaxCachedTooltips = 10_000;

    // The client's item names: 1020000 + graphic, and 1078872 + graphic from 0x4000 (ModernUO's LabelNumber).
    private const int ItemNameCliloc = 1020000;
    private const int HighItemNameCliloc = 1078872;
    private const int HighItemGraphic = 0x4000;
    private const int AmountAndNameCliloc = 1050039; // ~1_NUMBER~ ~2_ITEMNAME~
    private const int MobileNameCliloc = 1050045; // ~1_PREFIX~~2_NAME~~3_SUFFIX~

    private const byte CannotLiftWeight = 255;

    // messages/*.toml: the server's own tooltip texts, in the server language.
    private const int RarityMessageBase = 30000; // + rarity
    private const int BlessedMessage = 9055; // [Blessed], as UOX3
    private const int CursedMessage = 30005;
    private const int OneStoneMessage = 30006;
    private const int StonesMessage = 30007;

    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly ILocalizationService _localization;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly WorldConfig _world;
    private readonly ConcurrentDictionary<ItemTooltipKey, PropertyList> _itemTooltips = new();
    private readonly ConcurrentDictionary<MobileTooltipKey, PropertyList> _mobileTooltips = new();

    public TooltipService(
        IItemTemplateService templates,
        ITileDataService tiles,
        ILocalizationService localization,
        IItemService items,
        IMobileService mobiles,
        WorldConfig world
    )
    {
        _templates = templates;
        _tiles = tiles;
        _localization = localization;
        _items = items;
        _mobiles = mobiles;
        _world = world;
    }

    public PropertyListInfoPacket Info(ItemEntity item)
    {
        return new(item.Id, Build(item).Hash);
    }

    public PropertyListInfoPacket Info(MobileEntity mobile)
    {
        return new(mobile.Id, Build(mobile).Hash);
    }

    public bool TryBuildFor(Serial viewer, Serial target, [NotNullWhen(true)] out PropertyList? list)
    {
        list = null;

        if (!_mobiles.TryGet(viewer, out var character))
        {
            return false;
        }

        if (_mobiles.TryGet(target, out var mobile))
        {
            if (!InView(character, mobile.Map, mobile.Location))
            {
                return false;
            }

            list = Build(mobile);

            return true;
        }

        if (!_items.TryGet(target, out var item) || !IsVisibleTo(character, item))
        {
            return false;
        }

        list = Build(item);

        return true;
    }

    public PropertyList Build(ItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var lootType = item.TryGetProp<LootType>(ItemPropKeys.LootType, out var own) ? own : (LootType?)null;
        var key = new ItemTooltipKey(item.TemplateId, item.ItemId, item.Amount, item.Name, item.Rarity, lootType, item.Movable);

        return Cached(_itemTooltips, key, () => BuildItem(item, lootType));
    }

    private PropertyList BuildItem(ItemEntity item, LootType? ownLootType)
    {
        var list = new PropertyList();
        _templates.TryGet(item.TemplateId, out var template);
        AddName(list, item, Argument(item.Name ?? template?.Name));

        var lootType = ownLootType ?? template?.EffectiveLootType() ?? LootType.Regular;

        if (lootType is LootType.Blessed or LootType.Newbied)
        {
            list.AddText(Text(BlessedMessage, "[Blessed]"));
        }
        else if (lootType == LootType.Cursed)
        {
            list.AddText(Text(CursedMessage, "[Cursed]"));
        }

        // As ModernUO: rounded up (a feather weighs a stone), and not shown for what cannot be picked up.
        if (item.Movable ?? template?.EffectiveMovable(_tiles) ?? TiledataWeight(item) < CannotLiftWeight)
        {
            var weight = (int)Math.Ceiling((template?.EffectiveWeight(_tiles) ?? TiledataWeight(item)) * item.Amount);
            list.AddText(weight == 1 ? Text(OneStoneMessage, "Weight: 1 stone") : Text(StonesMessage, "Weight: {0} stones", weight));
        }

        var rarity = Text(RarityMessageBase + (int)item.Rarity, item.Rarity.ToString());
        list.AddText($"<BASEFONT COLOR={RarityColor(item.Rarity)}>{rarity}</BASEFONT>");

        return list;
    }

    public PropertyList Build(MobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        return Cached(_mobileTooltips, new MobileTooltipKey(mobile.Name, mobile.Title), () => BuildMobile(mobile));
    }

    private static PropertyList Cached<TKey>(ConcurrentDictionary<TKey, PropertyList> cache, TKey key, Func<PropertyList> build)
        where TKey : notnull
    {
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (cache.Count >= MaxCachedTooltips)
        {
            cache.Clear();
        }

        return cache.GetOrAdd(key, _ => build());
    }

    private PropertyList BuildMobile(MobileEntity mobile)
    {
        // The client needs a single space for an empty prefix or suffix.
        var list = new PropertyList();
        var title = Argument(mobile.Title);
        list.Add(MobileNameCliloc, $" \t{Argument(mobile.Name) ?? " "}\t{(string.IsNullOrEmpty(title) ? " " : " " + title)}");

        return list;
    }

    // Carried or worn by the viewer, worn by a mobile it sees, or lying on the ground in view; never inside someone
    // else's containers.
    private bool IsVisibleTo(MobileEntity viewer, ItemEntity item)
    {
        if (_items.GetOwner(item) is { } owner)
        {
            return owner == viewer.Id ||
                   (item.MobileId == owner && _mobiles.TryGet(owner, out var wearer) && InView(viewer, wearer.Map, wearer.Location));
        }

        return item.Map is { } map &&
               item.GroundLocation is { } spot &&
               _items.IsLyingOnGround(item) &&
               InView(viewer, map, spot);
    }

    private bool InView(MobileEntity viewer, MapType map, Point3D location)
    {
        return viewer.Map == map &&
               Math.Abs(viewer.Location.X - location.X) <= _world.ViewRange &&
               Math.Abs(viewer.Location.Y - location.Y) <= _world.ViewRange;
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

    // A message file without the text gives the English one rather than failing the whole broadcast.
    private string Text(int id, string english, params object[] values)
    {
        return _localization.TryGetText(id, out _) ? _localization.Get(id, values) : string.Format(english, values);
    }

    // A tab would split the text into another cliloc argument.
    private static string? Argument(string? text)
    {
        return text?.Replace('\t', ' ');
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
            ItemRarityType.Common => "#FFFFFF",
            ItemRarityType.Uncommon => "#1EFF00",
            ItemRarityType.Rare => "#0070DD",
            ItemRarityType.Epic => "#A335EE",
            _ => "#FF8000"
        };
    }
}
