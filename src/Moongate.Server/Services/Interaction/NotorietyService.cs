using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Resolves classic UO notoriety hues from the viewer's perspective.
/// </summary>
public sealed class NotorietyService : INotorietyService
{
    private static readonly TimeSpan AggressionTimeout = TimeSpan.FromMinutes(2);
    private readonly IFactionTemplateService? _factionTemplateService;

    public NotorietyService(IFactionTemplateService? factionTemplateService = null)
    {
        _factionTemplateService = factionTemplateService;
    }

    public Notoriety Compute(UOMobileEntity source, UOMobileEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var nowUtc = DateTime.UtcNow;

        if (source.Id != Serial.Zero && source.Id == target.Id)
        {
            return Notoriety.Innocent;
        }

        if (target.IsBlessed || target.IsInvulnerable)
        {
            return Notoriety.Invulnerable;
        }

        if (target.Notoriety == Notoriety.Murdered)
        {
            return Notoriety.Murdered;
        }

        if (target.Notoriety == Notoriety.Criminal)
        {
            return Notoriety.Criminal;
        }

        if (HasRecentAggression(source, target, nowUtc) || HasRecentAggression(target, source, nowUtc))
        {
            return Notoriety.CanBeAttacked;
        }

        if (AreSameFaction(source, target))
        {
            return Notoriety.Innocent;
        }

        if (AreEnemyFactions(source, target))
        {
            return Notoriety.Enemy;
        }

        if (!target.IsPlayer &&
            (target.Body.IsMonster || target.Body.IsAnimal || target.Notoriety == Notoriety.Enemy))
        {
            return Notoriety.CanBeAttacked;
        }

        return target.Notoriety;
    }

    private static bool HasRecentAggression(UOMobileEntity source, UOMobileEntity target, DateTime nowUtc)
        => source.Aggressors.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc)) ||
           source.Aggressed.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc));

    private static bool MatchesRecentAggression(AggressorInfo entry, Serial targetId, DateTime nowUtc)
        => (entry.AttackerId == targetId || entry.DefenderId == targetId) &&
           nowUtc - entry.LastCombatAtUtc <= AggressionTimeout;

    private bool AreEnemyFactions(UOMobileEntity source, UOMobileEntity target)
    {
        if (_factionTemplateService is null ||
            string.IsNullOrWhiteSpace(source.FactionId) ||
            string.IsNullOrWhiteSpace(target.FactionId) ||
            AreSameFaction(source, target))
        {
            return false;
        }

        return FactionDeclaresEnemy(source.FactionId!, target.FactionId!) ||
               FactionDeclaresEnemy(target.FactionId!, source.FactionId!);
    }

    private static bool AreSameFaction(UOMobileEntity source, UOMobileEntity target)
        => !string.IsNullOrWhiteSpace(source.FactionId) &&
           !string.IsNullOrWhiteSpace(target.FactionId) &&
           string.Equals(source.FactionId, target.FactionId, StringComparison.OrdinalIgnoreCase);

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
