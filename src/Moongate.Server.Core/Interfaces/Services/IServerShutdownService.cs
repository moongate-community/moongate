namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Signals the host to exit its run phase and perform the normal ordered shutdown.
/// </summary>
public interface IServerShutdownService
{
    /// <summary>
    ///     Completes when shutdown is requested, not when service cleanup or the final world save finishes.
    /// </summary>
    Task Requested { get; }

    /// <summary>
    ///     Requests shutdown without waiting for it; repeated requests are harmless and any thread may call.
    /// </summary>
    void RequestShutdown();
}
