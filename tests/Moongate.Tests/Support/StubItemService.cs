using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Support;

/// <summary>
/// Minimal <see cref="IItemService" /> test double that only answers <see cref="GetEquipped" /> with a
/// fixed list; every other member is unused by the systems under test and throws if called.
/// </summary>
public sealed class StubItemService : IItemService
{
    private readonly IReadOnlyList<ItemEntity> _equipped;

    public StubItemService(IReadOnlyList<ItemEntity> equipped)
    {
        _equipped = equipped;
    }

    public void AddToContainer(ItemEntity container, ItemEntity item, Point2D position)
        => throw new NotSupportedException();

    public Serial Create(ItemEntity item)
        => throw new NotSupportedException();

    public bool Delete(Serial itemId)
        => throw new NotSupportedException();

    public void Equip(MobileEntity mobile, ItemEntity item, LayerType layer)
        => throw new NotSupportedException();

    public bool Flip(ItemEntity item)
        => throw new NotSupportedException();

    public ItemEntity? GetById(Serial itemId)
        => throw new NotSupportedException();

    public IReadOnlyList<ItemEntity> GetContents(Serial containerId)
        => throw new NotSupportedException();

    public IReadOnlyList<ItemEntity> GetEquipped(MobileEntity mobile)
        => _equipped;

    public void RemoveFromContainer(ItemEntity container, ItemEntity item)
        => throw new NotSupportedException();

    public void Save(ItemEntity item)
        => throw new NotSupportedException();

    public ItemEntity? Unequip(MobileEntity mobile, LayerType layer)
        => throw new NotSupportedException();
}
