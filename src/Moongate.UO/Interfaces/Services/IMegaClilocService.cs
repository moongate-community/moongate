using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.MegaCliloc;

namespace Moongate.UO.Interfaces.Services;

public interface IMegaClilocService : IMoongateAutostartService
{
    Task<MegaClilocEntry> GetMegaClilocEntryAsync(ISerialEntity entity);
}
