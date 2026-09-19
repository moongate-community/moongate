using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Tracks the game sessions currently registered with the server.</summary>
/// <remarks>The registry owns membership only; callers remain responsible for transport cleanup and session detachment.</remarks>
public interface ISessionService
{
    /// <summary>Gets the number of currently registered sessions.</summary>
    int Count { get; }

    /// <summary>Returns a snapshot of the currently registered session references.</summary>
    /// <remarks>Later registry changes do not alter membership in the returned collection.</remarks>
    IReadOnlyCollection<GameSession> GetAll();

    /// <summary>Returns the registered session for the client, creating it when absent.</summary>
    /// <remarks>A registered detached session is returned unchanged and is never reattached.</remarks>
    /// <exception cref="ArgumentNullException">The client is null.</exception>
    GameSession GetOrCreate(INetworkConnection client);

    /// <summary>Attempts to find a currently registered session by its transport session identifier.</summary>
    bool TryGet(long sessionId, [NotNullWhen(true)] out GameSession? session);

    /// <summary>Attempts to find a currently registered session with the current nonzero character association.</summary>
    /// <remarks>Character uniqueness is enforced by the future login flow, not by this lookup.</remarks>
    bool TryGetByCharacterId(Serial characterId, [NotNullWhen(true)] out GameSession? session);

    /// <summary>Removes a session from the registry without detaching it or closing its transport.</summary>
    bool Remove(long sessionId);

    /// <summary>Removes all registry membership without detaching sessions or closing transports.</summary>
    void Clear();
}
