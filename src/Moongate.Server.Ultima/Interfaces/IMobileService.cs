using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The mobiles in the world while the server runs: the live instances, the virtual serials the client needs for
///     things a mobile shows but that are not items, such as hair and beard, what the client is told about them, and
///     their steps.
/// </summary>
/// <remarks>
///     The live mobiles change on the game loop: move them only there. The world save writes their snapshots and deletes
///     the mobiles <see cref="Delete" /> took out.
/// </remarks>
public interface IMobileService : IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets the mobiles in the world.
    /// </summary>
    IReadOnlyCollection<Serial> InWorld { get; }

    /// <summary>
    ///     Gets the live mobiles in the world.
    /// </summary>
    IReadOnlyCollection<MobileEntity> Mobiles { get; }

    /// <summary>
    ///     Gets the virtual serial of the mobile's hair, the same every time while the server runs.
    /// </summary>
    Serial HairSerial(Serial mobile);

    /// <summary>
    ///     Gets the virtual serial of the mobile's beard, the same every time while the server runs.
    /// </summary>
    Serial BeardSerial(Serial mobile);

    /// <summary>
    ///     Keeps the mobile as the live one in the world; it replaces an instance with the same serial.
    /// </summary>
    void EnterWorld(MobileEntity mobile);

    /// <summary>
    ///     Gets the live mobile with the serial, when it is in the world.
    /// </summary>
    bool TryGet(Serial serial, [NotNullWhen(true)] out MobileEntity? mobile);

    /// <summary>
    ///     Forgets the mobile; false when it was not in the world.
    /// </summary>
    bool LeaveWorld(Serial serial);

    /// <summary>
    ///     Takes the mobile out of the world and queues its row for deletion by the next world save, in the same
    ///     transaction as the live mobiles; its item rows go with it through the database's cascading keys. False when it
    ///     is not in the world.
    /// </summary>
    /// <remarks>
    ///     Remove its live items from <see cref="IItemService" /> first: the save writes the mobiles before the items, so
    ///     a live item of a deleted mobile would be written back pointing at a missing row, and every world save would
    ///     fail.
    /// </remarks>
    bool Delete(Serial serial);

    /// <summary>
    ///     Turns the mobile towards <paramref name="direction" /> or, when it already faces that way, steps it to the next
    ///     cell if <see cref="IMovementService" /> allows it for <paramref name="ability" />, walking by default. The running
    ///     bit is ignored.
    /// </summary>
    /// <remarks>
    ///     Every change of a mobile's location must go through this service, as this method does, so the sectors, the NPC
    ///     senses and the players' regions follow it; a future teleport belongs here too.
    /// </remarks>
    MoveResultType TryMove(
        MobileEntity mobile,
        DirectionType direction,
        MovementAbilityType ability = MovementAbilityType.Walk
    );

    /// <summary>
    ///     Gets whether the mobile is in the world.
    /// </summary>
    bool IsInWorld(Serial mobile);

    /// <summary>
    ///     Gets the flags the client draws the mobile with (0x77, 0x78): female today.
    /// </summary>
    MobileFlagsType GetFlags(MobileEntity mobile);

    /// <summary>
    ///     Gets what the status bar shows for the mobile (0x11).
    /// </summary>
    MobileStatusInfo GetStatus(MobileEntity mobile);

    /// <summary>
    ///     Gets what the mobile wears as the client draws it (0x78): the worn items, one per layer, then its hair and
    ///     beard as virtual items unless an item already holds their layer.
    /// </summary>
    List<MobileEquipmentEntry> GetEquipment(MobileEntity mobile, IEnumerable<ItemEntity> worn);
}
