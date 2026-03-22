using Moongate.Server.Data.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Applies stat gain rules triggered by successful skill gains.
/// </summary>
public interface IStatGainService
{
    /// <summary>
    /// Attempts to apply a stat gain for the specified skill.
    /// </summary>
    /// <param name="mobile">The mobile whose stats may change.</param>
    /// <param name="skillName">The skill that triggered the stat gain attempt.</param>
    /// <returns>The applied stat gain result.</returns>
    StatGainResult TryApply(UOMobileEntity mobile, UOSkillName skillName);
}
