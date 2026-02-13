using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Factory;

namespace Moongate.UO.Interfaces.Services;

public interface INameService : IMoongateService
{
    void AddNames(string type, params string[] names);
    string GenerateName(string type);

    string GenerateName(MobileTemplate template);
}
