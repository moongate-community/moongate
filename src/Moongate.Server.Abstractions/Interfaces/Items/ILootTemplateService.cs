using Moongate.UO.Data.Loot;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>Stores loaded loot templates for case-insensitive lookup.</summary>
public interface ILootTemplateService
{
    /// <summary>All registered templates ordered by ID.</summary>
    IReadOnlyList<LootTemplate> All { get; }

    /// <summary>Number of registered templates.</summary>
    int Count { get; }

    /// <summary>Returns a template by ID, or <c>null</c> when it is not registered.</summary>
    LootTemplate? GetById(string id);

    /// <summary>Adds or replaces a template by its case-insensitive ID.</summary>
    void Register(LootTemplate lootTemplate);
}
