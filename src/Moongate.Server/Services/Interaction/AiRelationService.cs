using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Resolves coarse-grained AI relationships between two mobiles.
/// </summary>
public sealed class AiRelationService : IAiRelationService
{
    private static readonly TimeSpan AggressionTimeout = TimeSpan.FromMinutes(2);
    private readonly IFactionTemplateService? _factionTemplateService;

    public AiRelationService(IFactionTemplateService? factionTemplateService = null)
    {
        _factionTemplateService = factionTemplateService;
    }

    public AiRelation Compute(UOMobileEntity viewer, UOMobileEntity target)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(target);
        var nowUtc = DateTime.UtcNow;

        if (viewer.Id == target.Id)
        {
            return AiRelation.Friendly;
        }

        if (HasRecentAggression(viewer, target, nowUtc) || HasRecentAggression(target, viewer, nowUtc))
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

        if (AreSameFaction(viewer, target))
        {
            return AiRelation.Friendly;
        }

        if (AreEnemyFactions(viewer, target))
        {
            return AiRelation.Hostile;
        }

        if (IsHostileNpcViewer(viewer) && target.IsPlayer)
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

    private static bool HasRecentAggression(UOMobileEntity viewer, UOMobileEntity target, DateTime nowUtc)
        => viewer.Aggressors.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc)) ||
           viewer.Aggressed.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc));

    private static bool MatchesRecentAggression(AggressorInfo entry, Serial targetId, DateTime nowUtc)
        => (entry.AttackerId == targetId || entry.DefenderId == targetId) &&
           nowUtc - entry.LastCombatAtUtc <= AggressionTimeout;

    private static bool IsHostileNpcViewer(UOMobileEntity viewer)
        => !viewer.IsPlayer &&
           (viewer.Body.IsMonster ||
            viewer.Body.IsAnimal ||
            viewer.Notoriety is Notoriety.CanBeAttacked or
                Notoriety.Enemy or
                Notoriety.Criminal or
                Notoriety.Murdered);

    private bool AreEnemyFactions(UOMobileEntity viewer, UOMobileEntity target)
    {
        if (_factionTemplateService is null ||
            string.IsNullOrWhiteSpace(viewer.FactionId) ||
            string.IsNullOrWhiteSpace(target.FactionId) ||
            AreSameFaction(viewer, target))
        {
            return false;
        }

        return FactionDeclaresEnemy(viewer.FactionId!, target.FactionId!) ||
               FactionDeclaresEnemy(target.FactionId!, viewer.FactionId!);
    }

    private static bool AreSameFaction(UOMobileEntity viewer, UOMobileEntity target)
        => !string.IsNullOrWhiteSpace(viewer.FactionId) &&
           !string.IsNullOrWhiteSpace(target.FactionId) &&
           string.Equals(viewer.FactionId, target.FactionId, StringComparison.OrdinalIgnoreCase);

    private bool FactionDeclaresEnemy(string factionId, string enemyFactionId)
    {
        if (_factionTemplateService is null ||
            !_factionTemplateService.TryGet(factionId, out var faction) ||
            faction is null)
        {
            return false;
        }

        return faction.EnemyFactionIds.Any(
            declaredEnemy => string.Equals(declaredEnemy, enemyFactionId, StringComparison.OrdinalIgnoreCase)
        );
    }
}
