using System.Text.Json;
using Moongate.Server.Data.Entities;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Resolves startup placeholder tokens inside JSON args payloads.
/// </summary>
public interface IPlaceholderResolverService
{
    /// <summary>
    /// Resolves known placeholder tokens in the provided args payload.
    /// </summary>
    /// <param name="args">Source args payload.</param>
    /// <param name="profileContext">Starter profile context.</param>
    /// <param name="playerName">Player name value for token replacement.</param>
    /// <returns>Resolved args payload.</returns>
    JsonElement Resolve(JsonElement args, StarterProfileContext profileContext, string playerName);
}
