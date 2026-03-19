using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Resolves classic UO notoriety hues from the viewer's perspective.
/// </summary>
public sealed class NotorietyService : INotorietyService
{
    public Notoriety Compute(UOMobileEntity source, UOMobileEntity target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

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

        if (!target.IsPlayer &&
            (target.Body.IsMonster || target.Body.IsAnimal || target.Notoriety == Notoriety.Enemy))
        {
            return Notoriety.CanBeAttacked;
        }

        return target.Notoriety;
    }
}
