using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc />
/// <remarks>
///     A two-handed weapon is an item whose template has <c>two_handed_weapon = true</c>; anything else on
///     <see cref="LayerType.TwoHanded" /> (a shield, a torch) is held in the other hand.
/// </remarks>
public sealed class EquipmentService : IEquipmentService
{
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly IItemService _items;

    public EquipmentService(IItemTemplateService templates, ITileDataService tiles, IItemService items)
    {
        _templates = templates;
        _tiles = tiles;
        _items = items;
    }

    public bool TryGetLayer(ItemEntity item, out LayerType layer)
    {
        if (_templates.TryGet(item.TemplateId, out var template) && template.EffectiveLayer(_tiles) is { } resolved)
        {
            layer = resolved;

            return true;
        }

        if (_tiles.TryGetItem(item.ItemId, out var tile) &&
            (tile.Flags & TileFlagType.Wearable) != 0 &&
            (LayerType)tile.Layer != LayerType.None)
        {
            layer = (LayerType)tile.Layer;

            return true;
        }

        layer = LayerType.None;

        return false;
    }

    public bool CanWear(Serial mobile, ItemEntity item, LayerType layer)
    {
        if (!IsWornFromThePaperdoll(layer))
        {
            return false;
        }

        // The item itself may still be worn: picked up from the paperdoll, it stays on until it is dropped.
        var worn = _items.GetWorn(mobile).Where(other => other.Id != item.Id).ToList();

        if (worn.Any(other => other.Layer == layer))
        {
            return false;
        }

        if (layer is not (LayerType.OneHanded or LayerType.TwoHanded))
        {
            return true;
        }

        // A two-handed weapon needs both hands; while one is worn, nothing else goes in hand.
        var inHand = worn.Where(other => other.Layer is LayerType.OneHanded or LayerType.TwoHanded).ToList();

        return IsTwoHandedWeapon(item) ? inHand.Count == 0 : !inHand.Any(IsTwoHandedWeapon);
    }

    private bool IsTwoHandedWeapon(ItemEntity item)
    {
        return _templates.TryGet(item.TemplateId, out var template) && template.TwoHandedWeapon == true;
    }

    private static bool IsWornFromThePaperdoll(LayerType layer)
    {
        return layer is >= LayerType.OneHanded and
            <= LayerType.InnerLegs and
            not (LayerType.Hair or LayerType.FacialHair or LayerType.Backpack);
    }
}
