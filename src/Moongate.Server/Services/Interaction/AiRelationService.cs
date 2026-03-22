using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Resolves coarse-grained AI relationships between two mobiles.
/// </summary>
public sealed class AiRelationService : IAiRelationService
{
    public AiRelation Compute(UOMobileEntity viewer, UOMobileEntity target)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(target);

        if (viewer.Id == target.Id)
        {
            return AiRelation.Friendly;
        }

        if (HasRecentAggression(viewer, target) || HasRecentAggression(target, viewer))
        {
            return AiRelation.Hostile;
        }

        if (target.IsBlessed || target.IsInvulnerable)
        {
            return AiRelation.Neutral;
        }

        if (target.Notoriety is Notoriety.Criminal or Notoriety.Murdered)
        {
            return AiRelation.Hostile;
        }

        if (!target.IsPlayer &&
            (target.Body.IsMonster ||
             target.Body.IsAnimal ||
             target.Notoriety is Notoriety.CanBeAttacked or Notoriety.Enemy))
        {
            return AiRelation.Hostile;
        }

        return AiRelation.Neutral;
    }

    private static bool HasRecentAggression(UOMobileEntity viewer, UOMobileEntity target)
        => viewer.Aggressors.Any(entry => entry.AttackerId == target.Id || entry.DefenderId == target.Id) ||
           viewer.Aggressed.Any(entry => entry.AttackerId == target.Id || entry.DefenderId == target.Id);
}
