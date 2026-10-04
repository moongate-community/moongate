using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells a player the place it walks into and out of, and whether guards protect it there: "You have entered
///     Britain.", "You are now under the protection of the guards of Britain." It follows the players through the
///     region changes and says nothing before a player's login completes.
/// </summary>
public interface IRegionAnnouncer : IMoongateStartupService, IRegionChangeListener
{
}
