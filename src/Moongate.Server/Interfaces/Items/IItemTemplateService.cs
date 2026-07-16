using Moongate.UO.Data.Items;

namespace Moongate.Server.Interfaces.Items;

/// <summary>In-memory registry of item templates, queryable by id.</summary>
public interface IItemTemplateService
{
    /// <summary>All registered item templates.</summary>
    IReadOnlyList<ItemTemplate> All { get; }

    /// <summary>Number of registered item templates.</summary>
    int Count { get; }

    /// <summary>Returns the item template with the given id (case-insensitive), or null.</summary>
    ItemTemplate? GetById(string id);

    /// <summary>Adds or replaces an item template, indexed by id.</summary>
    void Register(ItemTemplate itemTemplate);
}
