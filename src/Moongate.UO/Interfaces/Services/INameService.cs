using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Factory;

namespace Moongate.UO.Interfaces.Services;

public interface INameService : IMoongateService
{
    string GenerateName(string type);

    void AddNames(string type, params string[] names);

    string GenerateName(MobileTemplate template);
}
