using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.TestSupport.Services;

public class StartupRegistrationService : IRegistrationService, IMoongateStartupService
{
    public RegistrationDependency Dependency { get; }

    public StartupRegistrationService(RegistrationDependency dependency)
    {
        Dependency = dependency;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
