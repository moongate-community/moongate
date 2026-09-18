namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns the TCP listeners that accept game clients.</summary>
/// <remarks>
/// Starting binds a listener to every configured endpoint and registers each accepted client with the
/// session service. If one listener fails to start, those already open are closed and the failure
/// propagates. Stopping waits for any startup in progress to settle before closing every listener,
/// and starting again after a stop has begun is rejected.
/// </remarks>
public interface INetworkService : IMoongateStartupService
{
}
