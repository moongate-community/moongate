using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services;

public interface IMobileService : IMoongateAutostartService
{
    UOMobileEntity CreateMobile();
}
