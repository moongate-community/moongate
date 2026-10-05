using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
namespace Moongate.Server.Ultima.Interfaces.Items;
/// <summary>Preflights inventory changes before scripts, allocation or client effects.</summary>
public interface IInventoryMutationGuard
{
    /// <summary>Checks the item's ancestry and optional destination for owner exclusion.</summary>
    bool Allows(ItemEntity item, Serial? destination = null);
    /// <summary>Checks whether an owner permits inventory changes.</summary>
    bool AllowsOwner(Serial mobileId);
}
