using Moongate.Server.Interfaces;
using Moongate.UO.Data.Containers;

namespace Moongate.Server.Services.Items;

/// <summary>
/// In-memory registry of container gump layouts, queryable by gump id or item id. Populated at
/// startup by <see cref="Moongate.Server.Loaders.ContainerGumpsLoader" />.
/// </summary>
public sealed class ContainerGumpService : IContainerGumpService
{
    private readonly Dictionary<int, ContainerGumpLayout> _byGumpId = new();
    private readonly Dictionary<int, ContainerGumpLayout> _byItemId = new();

    public IReadOnlyList<ContainerGumpLayout> All => [.. _byGumpId.Values.OrderBy(layout => layout.GumpId)];

    public int Count => _byGumpId.Count;

    public void Register(ContainerGumpLayout layout)
    {
        _byGumpId[layout.GumpId] = layout;

        foreach (var itemId in layout.ItemIds)
        {
            _byItemId[itemId] = layout;
        }
    }

    public ContainerGumpLayout? GetByGumpId(int gumpId)
    {
        return _byGumpId.GetValueOrDefault(gumpId);
    }

    public ContainerGumpLayout? GetByItemId(int itemId)
    {
        return _byItemId.GetValueOrDefault(itemId);
    }
}
