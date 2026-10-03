using Moongate.Core.Primitives;
using Moongate.Core.Geometry;
using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells the players in range what they see of each other: who appears (0x78), moves or turns (0x77) and leaves
///     their view (0x1D), recomputed from the old and new position of each step as ModernUO and POL do.
/// </summary>
/// <remarks>
///     Game loop only. Only mobiles registered through <see cref="Entered" /> receive packets.
/// </remarks>
public interface IWorldViewService
{
    /// <summary>
    ///     Registers the player's session, shows it everyone and every ground item in range and shows it to the players in
    ///     range. <paramref name="version" /> is its client's, null when unknown (the newest formats);
    ///     <paramref name="account" /> decides which hidden items it sees, such as the teleporters only staff sees.
    /// </summary>
    void Entered(MobileEntity mobile, long sessionId, ClientVersion? version, AccountType account = AccountType.Regular);

    /// <summary>
    ///     Tells the players in range that the mobile, now at its current location, moved from
    ///     <paramref name="oldLocation" /> or turned on the spot.
    /// </summary>
    void Moved(MobileEntity mobile, Point3D oldLocation, bool running);

    /// <summary>
    ///     Tells the players in range that the mobile, now at its current map and location, jumped there from
    ///     <paramref name="oldLocation" /> of <paramref name="oldMap" />. On the same map: as <see cref="Moved" />, but
    ///     those who still see it are shown it again (0x78) instead of a step (0x77), as ModernUO does for a teleport.
    ///     From another map: the players around the old spot lose it and it loses them and the ground items there
    ///     (0x1D), and on the new map it is shown everyone and every ground item in range and shown to the players
    ///     there, as when it entered the world.
    /// </summary>
    void Teleported(MobileEntity mobile, MapType oldMap, Point3D oldLocation);

    /// <summary>
    ///     Removes the mobile from the screens in range, registered or not, and forgets its session; call it while the
    ///     mobile is still in the sector grid.
    /// </summary>
    void Left(MobileEntity mobile);

    /// <summary>
    ///     Shows a mobile that just came into the world, such as a spawned NPC, to the players in range (0x78).
    /// </summary>
    void MobileAppeared(MobileEntity mobile);

    /// <summary>
    ///     Shows a ground item to the players in range whose account sees it (0x1A before client 7.0.0.0, 0xF3 after);
    ///     also after its amount changed.
    /// </summary>
    void ItemAppeared(ItemEntity item);

    /// <summary>
    ///     Shows a ground item to one player only, such as the one whose lift of it was refused.
    /// </summary>
    void ShowItemTo(MobileEntity viewer, ItemEntity item);

    /// <summary>
    ///     Removes a ground item from the screens in range (0x1D); call it when it is lifted, merged or deleted, with its
    ///     location still set.
    /// </summary>
    void ItemDisappeared(ItemEntity item);

    /// <summary>
    ///     Shows an item put into <paramref name="root" />, a container lying on the ground, or into a container inside
    ///     it, to the players in range of it (0x25), so those who look into it see the item come; not to
    ///     <paramref name="except" />, who put it there and is told by the caller.
    /// </summary>
    void ContainedItemAppeared(ItemEntity item, ItemEntity root, Serial except);

    /// <summary>
    ///     Removes an item taken from <paramref name="root" />, a container lying on the ground, or from a container
    ///     inside it, from the screens in range of it (0x1D), so those who look into it see the item go; not from
    ///     that of <paramref name="except" />, who took it.
    /// </summary>
    void ContainedItemDisappeared(ItemEntity item, ItemEntity root, Serial except);

    /// <summary>
    ///     Shows an item now worn by <paramref name="wearer" /> (0x2E) to every player in range, the wearer included.
    /// </summary>
    void WornItemChanged(MobileEntity wearer, ItemEntity item);

    /// <summary>
    ///     Takes an item off <paramref name="wearer" /> (0x1D) for the other players in range; the wearer's client
    ///     already took it off when it was picked up.
    /// </summary>
    void WornItemRemoved(MobileEntity wearer, ItemEntity item);
}
