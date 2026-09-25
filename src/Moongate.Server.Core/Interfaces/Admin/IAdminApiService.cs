using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Core.Interfaces.Admin;

/// <summary>
///     Owns the optional administration listener within the server lifecycle.
/// </summary>
public interface IAdminApiService : IMoongateStartupService
{
    /// <summary>
    ///     Accepts calls only after every startup subscriber and runtime activation has completed.
    /// </summary>
    void Activate();

    /// <summary>
    ///     Rejects new calls before shutdown begins; StopAsync drains existing calls.
    /// </summary>
    void StopAccepting();
}
