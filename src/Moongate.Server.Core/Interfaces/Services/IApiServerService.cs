using System.Net;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns the optional internal API listener within the server startup and shutdown lifecycle.</summary>
public interface IApiServerService : IMoongateStartupService
{
    /// <summary>Gets the actual bound endpoint while accepting connections, otherwise null.</summary>
    IPEndPoint? Endpoint { get; }
}
