using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Applies fame and karma awards after player-versus-NPC kills.
/// </summary>
public sealed class FameKarmaService : IFameKarmaService
{
    private readonly IMobileService _mobileService;

    public FameKarmaService(IMobileService mobileService)
    {
        _mobileService = mobileService;
    }

    public async Task AwardNpcKillAsync(
        UOMobileEntity victim,
        UOMobileEntity killer,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(victim);
        ArgumentNullException.ThrowIfNull(killer);

        if (victim.IsPlayer || !killer.IsPlayer)
        {
            return;
        }

        var awardedFame = Math.Max(0, victim.Fame / 100);
        var awardedKarma = victim.Karma / 100;

        if (awardedFame == 0 && awardedKarma == 0)
        {
            return;
        }

        killer.Fame += awardedFame;
        killer.Karma += awardedKarma;

        await _mobileService.CreateOrUpdateAsync(killer, cancellationToken);
    }
}
