using Moongate.Core.Server.Data.Version;
using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface IVersionService : IMoongateAutostartService
{
    VersionInfoData GetVersionInfo();
}
