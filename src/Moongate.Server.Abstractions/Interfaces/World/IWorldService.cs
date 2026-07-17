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
}
