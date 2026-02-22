using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Startup;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Composes startup loadout entries by applying base, race, gender and profession startup templates.
/// </summary>
public interface IStartupCompositionService
{
    /// <summary>
    /// Composes startup loadout entries for the given profile context and player name.
    /// </summary>
    /// <param name="profileContext">Starter profile context.</param>
    /// <param name="playerName">Player name used for placeholder resolution.</param>
    /// <returns>Composed startup loadout.</returns>
    StartupLoadout Compose(StarterProfileContext profileContext, string playerName);
}
