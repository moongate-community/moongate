namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Coordinates loop-thread captures with asynchronous durable persistence and backups.</summary>
public interface IWorldSaveService : IMoongateStartupService
{
    /// <summary>Enables requests and autosaving after the complete server-started publication returns successfully.</summary>
    void Activate();

    /// <summary>Requests a save or joins the active capture and durable save.</summary>
    /// <remarks>Joined requests share the current capture. Cancellation cancels only this caller's wait.</remarks>
    /// <exception cref="InvalidOperationException">Startup has not activated saving, or shutdown has begun.</exception>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Drains active saving and optionally captures a final world after closing the game loop.</summary>
    /// <remarks>Parameterless StopAsync is cleanup-only. A cleanup-only stop cannot later be upgraded to a final save.</remarks>
    Task StopAsync(bool saveFinal);
}
