using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Services.Interfaces;

/// <summary>
///     Test contract for resolving a service with a required dependency.
/// </summary>
public interface IRegistrationService : IMoongateService
{
    /// <summary>
    ///     Gets the dependency supplied when the service is resolved.
    /// </summary>
    RegistrationDependency Dependency { get; }
}
