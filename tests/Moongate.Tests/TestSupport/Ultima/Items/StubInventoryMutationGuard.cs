using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces.Items;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     An inventory guard that allows everything, or, when a test says so, nothing carried, as a pending reservation of
///     the inventory does: what lies on the ground stays allowed.
/// </summary>
public sealed class StubInventoryMutationGuard : IInventoryMutationGuard
{
    public bool Allowed { get; set; } = true;

    public bool Allows(ItemEntity item, Serial? destination = null)
    {
        return Allowed || (item.ContainerId is null && item.MobileId is null && destination is null);
    }

    public bool AllowsOwner(Serial mobileId)
    {
        return Allowed;
    }
}
