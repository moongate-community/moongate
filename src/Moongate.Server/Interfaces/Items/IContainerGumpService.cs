using Moongate.UO.Data.Containers;

namespace Moongate.Server.Interfaces.Items;

/// <summary>In-memory registry of container gump layouts, queryable by gump id or item id.</summary>
public interface IContainerGumpService
{
    /// <summary>All registered gump layouts, ordered by gump id.</summary>
    IReadOnlyList<ContainerGumpLayout> All { get; }

    /// <summary>Number of registered gump layouts.</summary>
    int Count { get; }

    /// <summary>Returns the gump layout with the given gump id, or null.</summary>
    ContainerGumpLayout? GetByGumpId(int gumpId);

    /// <summary>Returns the gump layout used by the given container item id, or null.</summary>
    ContainerGumpLayout? GetByItemId(int itemId);

    /// <summary>Adds or replaces a gump layout, indexed by gump id.</summary>
    void Register(ContainerGumpLayout layout);
}
