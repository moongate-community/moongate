using Moongate.Server.Data.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Applies combat-driven skill gain rules to mobiles.
/// </summary>
public interface ISkillGainService
{
    /// <summary>
    /// Attempts to increase the requested skill using the supplied action difficulty context.
    /// </summary>
    /// <param name="mobile">The mobile whose skill should be evaluated.</param>
    /// <param name="skillName">The skill being trained.</param>
    /// <param name="successChance">The normalized success chance for the action.</param>
    /// <param name="wasSuccessful"><c>true</c> when the action succeeded; otherwise <c>false</c>.</param>
    /// <returns>The applied gain result.</returns>
    SkillGainResult TryGain(UOMobileEntity mobile, UOSkillName skillName, double successChance, bool wasSuccessful);
}
