using Moongate.Server.Data.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Applies runtime anti-macro rules to skill gain attempts.
/// </summary>
public interface ISkillAntiMacroService
{
    /// <summary>
    /// Determines whether the current gain attempt should remain eligible.
    /// </summary>
    /// <param name="mobile">The mobile attempting to gain the skill.</param>
    /// <param name="skillName">The skill being trained.</param>
    /// <param name="context">The action context for the gain attempt.</param>
    /// <returns><c>true</c> when gain may continue; otherwise <c>false</c>.</returns>
    bool AllowGain(UOMobileEntity mobile, UOSkillName skillName, SkillGainContext? context);
}
