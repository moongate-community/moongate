using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Entities;

public interface IMobileModifierAggregationService
{
    MobileModifiers RecalculateEquipmentModifiers(UOMobileEntity mobile);
}
