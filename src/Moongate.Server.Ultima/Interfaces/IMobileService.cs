using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The mobiles in the world while the server runs: which are there, the virtual serials the client needs for
///     things a mobile shows but that are not items, such as hair and beard, and what the client is told about them.
/// </summary>
/// <remarks>
///     Everything here lives in memory and is not saved. Members may be called from any thread.
/// </remarks>
public interface IMobileService
{
    /// <summary>
    ///     Gets the mobiles in the world.
    /// </summary>
    IReadOnlyCollection<Serial> InWorld { get; }

    /// <summary>
    ///     Gets the virtual serial of the mobile's hair, the same every time while the server runs.
    /// </summary>
    Serial HairSerial(Serial mobile);

    /// <summary>
    ///     Gets the virtual serial of the mobile's beard, the same every time while the server runs.
    /// </summary>
    Serial BeardSerial(Serial mobile);

    /// <summary>
    ///     Records that the mobile is in the world.
    /// </summary>
    void EnterWorld(Serial mobile);

    /// <summary>
    ///     Gets whether the mobile is in the world.
    /// </summary>
    bool IsInWorld(Serial mobile);

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
