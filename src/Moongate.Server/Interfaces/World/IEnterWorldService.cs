using Moongate.Persistence.Entities;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Interfaces.World;

/// <summary>
/// Sends the "enter world" packet burst that puts a character into the game after it is selected
/// (0x5D) or created (0xF8), so the client renders the player standing in the world.
/// </summary>
public interface IEnterWorldService
{
    /// <summary>Streams the enter-world sequence for <paramref name="mobile" /> to <paramref name="session" />.</summary>
    void SendEnterWorld(PlayerSession session, MobileEntity mobile);
}
