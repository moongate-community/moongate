using Moongate.Server.Core.Data.Network.Events;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns protocol-configured listeners and transport cleanup without requiring game services.</summary>
/// <remarks>Startup is one-shot. Stop joins all transport callbacks and cleanup, but not game-session retirement.</remarks>
public interface INetworkService : IMoongateStartupService
{
    /// <summary>Raised synchronously after admission to the connection registry, before receiving data.</summary>
    event EventHandler<NetworkConnectionEventArgs>? ConnectionAccepted;

    /// <summary>Raised synchronously when an admitted connection closes. Cleanup may still be in progress.</summary>
    event EventHandler<NetworkConnectionEventArgs>? ConnectionClosed;

    /// <summary>Raised synchronously for bytes or a configured frame. Memory is borrowed until callback return.</summary>
    event EventHandler<NetworkDataEventArgs>? DataReceived;
}
