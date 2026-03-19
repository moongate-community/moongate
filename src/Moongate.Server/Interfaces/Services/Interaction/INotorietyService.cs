using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Computes the notoriety hue a source mobile should see for a target mobile.
/// </summary>
public interface INotorietyService
{
    Notoriety Compute(UOMobileEntity source, UOMobileEntity target);
}
