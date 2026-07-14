using Moongate.Persistence.Entities;
using Moongate.UO.Data.Hues;

namespace Moongate.Server.Interfaces.Items;

/// <summary>Builds unpersisted item entities from templates, by id, tag or category.</summary>
public interface IItemFactoryService
{
    /// <summary>Builds <paramref name="count" /> items from an exact template id; empty when the id is unknown.</summary>
    IReadOnlyList<ItemEntity> CreateFromTemplate(string templateId, int count = 1, int amount = 1, Hue? hue = null);

    /// <summary>Builds <paramref name="count" /> items, each a random template carrying the tag; empty when none match.</summary>
    IReadOnlyList<ItemEntity> CreateByTag(string tag, int count = 1, int amount = 1, Hue? hue = null);

    /// <summary>Builds <paramref name="count" /> items, each a random template in the category; empty when none match.</summary>
    IReadOnlyList<ItemEntity> CreateByCategory(string category, int count = 1, int amount = 1, Hue? hue = null);
}
