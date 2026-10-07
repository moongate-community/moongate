using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Items;

namespace Moongate.Server.Ultima.Services.Items;

/// <inheritdoc />
public sealed class InventoryMutationGuard : IInventoryMutationGuard
{
    private readonly Lazy<IItemService> _items;
    private readonly IInventoryReservationService _reservations;

    public InventoryMutationGuard(Lazy<IItemService> items, IInventoryReservationService reservations)
    {
        _items = items;
        _reservations = reservations;
    }

    public bool Allows(ItemEntity item, Serial? destination = null)
    {
        if (!AllowsChain(item))
        {
            return false;
        }

        if (destination is not { } target)
        {
            return true;
        }

        return target.IsMobile
            ? AllowsOwner(target)
            : _items.Value.TryGet(target, out var container) && AllowsChain(container);
    }

    public bool AllowsOwner(Serial mobileId)
    {
        return !_reservations.IsReserved(mobileId);
    }

    private bool AllowsChain(ItemEntity item)
    {
        var seen = new HashSet<Serial>();
        var current = item;
        while (seen.Add(current.Id))
        {
            if (current.MobileId is { } owner)
            {
                return AllowsOwner(owner);
            }

            if (current.ContainerId is not { } parent)
            {
                return true;
            }

            // Safe: the out value is only used when the lookup succeeds.
            if (!_items.Value.TryGet(parent, out current!))
            {
                return false;
            }
        }

        return false;
    }
}
