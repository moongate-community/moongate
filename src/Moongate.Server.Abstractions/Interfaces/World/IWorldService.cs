using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>
/// World-facing operations that act on players and the game world. Currently owns bringing a
/// character into the game after it is selected (0x5D) or created (0xF8); more world behaviour hangs
/// off this service as it grows.
/// </summary>
public interface IWorldService
{
    /// <summary>
    /// Streams the enter-world sequence for <paramref name="mobile" /> to <paramref name="session" />
    /// and raises <see cref="Data.Events.PlayerEnteredWorldEvent" />.
    /// </summary>
    void SendEnterWorld(PlayerSession session, MobileEntity mobile);

    /// <summary>
    /// Sends <paramref name="packet" /> to every in-world player session whose character is on
    /// <paramref name="mapId" /> within <paramref name="range" /> tiles of <paramref name="center" />,
    /// skipping the mobile identified by <paramref name="exclude" /> (typically the originator).
    /// Returns the number of recipients.
    /// </summary>
    int SendToPlayersInRange<TPacket>(int mapId, Point3D center, int range, TPacket packet, Serial? exclude = null)
        where TPacket : IOutgoingPacket;
}
