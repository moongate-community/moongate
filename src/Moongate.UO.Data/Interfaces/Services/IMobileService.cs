using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IMobileService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void MobileEventHandler(UOMobileEntity mobile);
    event MobileEventHandler? MobileCreated;
    event MobileEventHandler? MobileRemoved;

    UOMobileEntity CreateMobile();
    UOMobileEntity? GetMobile(Serial id);

    IEnumerable<UOMobileEntity> QueryMobiles(Func<UOMobileEntity, bool> predicate);
}
