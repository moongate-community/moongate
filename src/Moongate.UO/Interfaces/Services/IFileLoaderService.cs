using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Interfaces.FileLoaders;

namespace Moongate.UO.Interfaces.Services;

public interface IFileLoaderService : IMoongateAutostartService
{
    void AddFileLoader<T>() where T : IFileLoader;

    Task ExecuteLoadersAsync();
}
