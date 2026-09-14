using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.TestSupport.Services;

public class RegistrationService : IRegistrationService
{
    public RegistrationDependency Dependency { get; }

    public RegistrationService(RegistrationDependency dependency)
    {
        Dependency = dependency;
    }
}
