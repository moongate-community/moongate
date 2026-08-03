using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Support;

/// <summary>
/// Containerhood from the template alone. Tests run without a client directory, so the tiledata the
/// real rule reads is empty and its own fallback is exactly this.
/// </summary>
public sealed class StubContainerRule : IContainerRule
{
    public bool IsContainer(ItemEntity item, ItemTemplate? template)
        => string.Equals(template?.Category, "Container", StringComparison.OrdinalIgnoreCase);
}
