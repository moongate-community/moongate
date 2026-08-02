using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Support;

/// <summary>
/// Minimal <see cref="IItemService" /> test double for the read side. <see cref="GetEquipped" /> answers
/// with a fixed list; <see cref="GetById" /> and <see cref="GetContents" /> answer from items handed to
/// <see cref="Track" />, resolving containment through each entity's own <c>ContainedItemIds</c> exactly
/// as the real service does. Every mutating member throws, so a test that reaches one says so.
/// </summary>
public sealed class StubItemService : IItemService
{
    private readonly Dictionary<Serial, ItemEntity> _items = new();

    public StubItemService(IReadOnlyList<ItemEntity> equipped)
    {
        Equipped = [.. equipped];

        foreach (var item in equipped)
        {
            _items[item.Id] = item;
        }
    }

    /// <summary>What <see cref="GetEquipped" /> answers with. Mutable, so a test can dress a mobile.</summary>
    public List<ItemEntity> Equipped { get; }

    /// <summary>How many items are findable, which a test can use to mint distinct serials.</summary>
    public int TrackedCount => _items.Count;

    public void AddToContainer(ItemEntity container, ItemEntity item, Point2D position)
        => throw new NotSupportedException();

    public Serial Create(ItemEntity item)
        => throw new NotSupportedException();

    public bool Delete(Serial itemId)
        => throw new NotSupportedException();

    public void Detach(ItemEntity item)
        => throw new NotSupportedException();

    public void Equip(MobileEntity mobile, ItemEntity item, LayerType layer)
        => throw new NotSupportedException();

    public bool Flip(ItemEntity item)
        => throw new NotSupportedException();

    public ItemEntity? GetById(Serial itemId)
        => _items.GetValueOrDefault(itemId);

    public IReadOnlyList<ItemEntity> GetContents(Serial containerId)
        => GetById(containerId) is { } container
               ? [.. container.ContainedItemIds.Select(GetById).OfType<ItemEntity>()]
               : [];

    public IReadOnlyList<ItemEntity> GetEquipped(MobileEntity mobile)
        => Equipped;

    public void MoveToWorld(ItemEntity item, int mapId, Point3D position)
        => throw new NotSupportedException();

    public void RemoveFromContainer(ItemEntity container, ItemEntity item)
        => throw new NotSupportedException();

    public ItemEntity RootOf(ItemEntity item)
        => throw new NotSupportedException();

    public void Save(ItemEntity item)
        => throw new NotSupportedException();

    /// <summary>Makes an item findable by serial, so a container listing it can resolve it.</summary>
    public ItemEntity Track(ItemEntity item)
    {
        _items[item.Id] = item;

        return item;
    }

    public ItemEntity? Unequip(MobileEntity mobile, LayerType layer)
        => throw new NotSupportedException();
}
