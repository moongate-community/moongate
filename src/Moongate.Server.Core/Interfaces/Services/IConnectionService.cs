using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Interfaces.Client;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns role-local connection membership, send admission and asynchronous closure.</summary>
/// <remarks>Contains no game sessions. Shutdown permanently closes registration and joins actual transport cleanup.</remarks>
public interface IConnectionService : IMoongateStartupService
{
    /// <summary>Gets the number of owned connections, including those still closing.</summary>
    int Count { get; }

    /// <summary>Closes send admission immediately and returns the shared task joining closure and actual cleanup.</summary>
    /// <remarks>Unknown identifiers are a no-op. Never wait for this task inside a transport callback.</remarks>
    Task DisconnectAsync(long sessionId);

    /// <summary>Closes only the expected connection if its identifier has been reused.</summary>
    Task DisconnectAsync(long sessionId, INetworkConnection expectedConnection)
        => expectedConnection.CloseAsync();

    /// <summary>Returns an independent membership snapshot, including connections with unfinished cleanup.</summary>
    IReadOnlyCollection<INetworkConnection> GetAll();

    /// <summary>Finds a live connection still accepting sends; a closing connection is unavailable immediately.</summary>
    bool TryGet(long sessionId, [NotNullWhen(true)] out INetworkConnection? connection);

    /// <summary>Atomically finds an admitted connection and captures its owner-initiated disconnect signal.</summary>
    /// <remarks>
    /// The signal completes successfully when DisconnectAsync or StopAsync initiates closure of the live connection.
    /// Remote closure and cleanup of an already closed transport do not signal it. It remains valid after membership
    /// removal, allowing senders to distinguish interrupted writes from failures that themselves closed the transport.
    /// </remarks>
    bool TryGet(
        long sessionId,
        [NotNullWhen(true)] out INetworkConnection? connection,
        [NotNullWhen(true)] out Task? disconnectRequested
    );

    /// <summary>Registers a live connection while running, without replacing another connection with the same identifier.</summary>
    /// <remarks>Registering the same admitted connection again is idempotent. Rejected connections remain caller-owned.</remarks>
    bool TryRegister(INetworkConnection connection);
}
