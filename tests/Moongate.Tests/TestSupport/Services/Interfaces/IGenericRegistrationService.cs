using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Services.Interfaces;

/// <summary>
///     Test contract for verifying registration of a closed generic service.
/// </summary>
/// <typeparam name="T">
///     The type used to close the service contract.
/// </typeparam>
public interface IGenericRegistrationService<T> : IMoongateService
{
}
