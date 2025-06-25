using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Interfaces.Services;

public interface ITrackMobileService: IMoongateService
{
    void Track(UOMobileEntity mobile, bool isPlayer = false);
}
