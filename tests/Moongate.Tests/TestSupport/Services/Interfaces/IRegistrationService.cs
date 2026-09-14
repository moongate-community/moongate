using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Services.Interfaces;

public interface IRegistrationService : IMoongateService
{
    RegistrationDependency Dependency { get; }
}
