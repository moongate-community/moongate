using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services;

public interface IMobileService : IMoongateAutostartService, IPersistenceLoadSave

{
    UOMobileEntity CreateMobile();

    UOMobileEntity? GetMobile(Serial id);
}
