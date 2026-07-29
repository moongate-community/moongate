using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Support;

/// <summary>
/// Answers from the template alone. The real rule reads the client's tiledata, a process-wide static
/// another test may have loaded; a drag test wants its own registered templates to be the whole truth.
/// </summary>
public sealed class StubStackableRule : IStackableRule
{
    public bool IsStackable(ItemEntity item, ItemTemplate? template)
        => template?.Stackable == true;
}
