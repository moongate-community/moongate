using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.UO.Interfaces.Services;

public interface IPersistenceService : IMoongateAutostartService
{
    void RequestSave();
}
