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

    public ItemEntity? Give(MobileEntity owner, string template, int? amount = null)
    {
        throw new NotSupportedException();
    }

    public bool Consume(ItemEntity item, int amount = 1)
    {
        throw new NotSupportedException();
    }

    public bool Delete(ItemEntity item)
    {
        throw new NotSupportedException();
    }

    public void Refresh(ItemEntity item)
    {
        throw new NotSupportedException();
    }

    public bool IsHeld(ItemEntity item)
    {
        return false;
    }
}
