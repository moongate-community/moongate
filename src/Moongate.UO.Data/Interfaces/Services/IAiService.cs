using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Interfaces.Ai;

namespace Moongate.UO.Data.Interfaces.Services;

public interface IAiService : IMoongateAutostartService
{
    void AddBrain(string brainId, IAiBrainAction brainAction);
}
