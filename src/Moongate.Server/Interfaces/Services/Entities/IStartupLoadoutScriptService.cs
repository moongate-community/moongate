using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Startup;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Builds starter backpack and equipment loadouts from the Lua startup hook.
/// </summary>
public interface IStartupLoadoutScriptService
{
    /// <summary>
    /// Builds the starting loadout for a character creation profile.
    /// </summary>
    /// <param name="profileContext">Starter profile context.</param>
    /// <param name="playerName">Player name used during character creation.</param>
    /// <returns>Resolved startup loadout.</returns>
    StartupLoadout BuildLoadout(StarterProfileContext profileContext, string playerName);
}
