using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Makes an item only for the template ids it was given, with the next serial of a counter, and does nothing else.
/// </summary>
public sealed class StubItemHandlingService : IItemHandlingService
{
    private uint _next = 0x40000500;

    /// <summary>
    ///     Gets the graphic of each template that can be made, by template id.
    /// </summary>
    public Dictionary<string, int> Templates { get; } = [];

    public ItemEntity? Make(string template, int? amount = null)
    {
        return Templates.TryGetValue(template, out var graphic)
            ? new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = graphic, Amount = amount ?? 1 }
            : null;
    }

    /// <summary>
    ///     Gets or sets whether <see cref="Give" /> finds no room in the backpack.
    /// </summary>
    public bool BackpackFull { get; set; }

    /// <summary>
    ///     Gets or sets whether <see cref="Give" /> finds no backpack at all.
    /// </summary>
    public bool NoBackpack { get; set; }

    public List<string> Refreshed { get; } = [];

    public List<ItemEntity> Given { get; } = [];

    public List<ItemEntity> Deleted { get; } = [];

    public ItemEntity? Give(MobileEntity owner, string template, int? amount = null, bool ignoreCapacity = false, Hue? hue = null)
    {
        if (NoBackpack || (BackpackFull && !ignoreCapacity) || Make(template, amount) is not { } item)
        {
            return null;
        }

        if (hue is { } colour)
        {
            item.Hue = colour;
        }

        Given.Add(item);

        return item;
    }

    public bool Consume(ItemEntity item, int amount = 1)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    ///     Gets or sets whether <see cref="Delete" /> is refused, as for an item held on the cursor.
    /// </summary>
    public bool DeleteFails { get; set; }

    public bool Delete(ItemEntity item)
    {
        if (DeleteFails)
        {
            return false;
        }

        Deleted.Add(item);

        return true;
    }

    public void Refresh(ItemEntity item)
    {
        Refreshed.Add(item.TemplateId ?? "");
    }

    public bool IsHeld(ItemEntity item)
    {
        return false;
    }
}
