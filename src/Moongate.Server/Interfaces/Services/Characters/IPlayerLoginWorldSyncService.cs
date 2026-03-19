using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Characters;

public interface IPlayerLoginWorldSyncService
{
    Task SyncAsync(GameSession session, UOMobileEntity mobileEntity, CancellationToken cancellationToken = default);
}
