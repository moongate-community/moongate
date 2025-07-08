using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IMobileService : IMoongateAutostartService, IPersistenceLoadSave
{
    delegate void MobileEventHandler(UOMobileEntity mobile);

    delegate void MobileMovedEventHandler(
        UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation
    );

    event MobileEventHandler? MobileCreated;
    event MobileEventHandler? MobileRemoved;
    event MobileEventHandler? MobileAdded;

    event MobileMovedEventHandler? MobileMoved;

    void AddInWorld(UOMobileEntity mobile);
    UOMobileEntity CreateMobile();
    UOMobileEntity? GetMobile(Serial id);

    IEnumerable<UOMobileEntity> QueryMobiles(Func<UOMobileEntity, bool> predicate);
}
