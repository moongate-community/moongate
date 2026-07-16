using Moongate.Core.Primitives;
using Moongate.Server.Data.Session;
using SquidStd.Network.Client;

namespace Moongate.Server.Interfaces.Accounts;

/// <summary>Owns the live client sessions keyed by connection id.</summary>
public interface ISessionManager
{
    int Count { get; }
    PlayerSession GetOrCreate(SquidStdTcpClient client);

    /// <summary>
    /// True when a live session has claimed <paramref name="mobileId" /> — selected it, or already taken
    /// it into the world. This is our answer to ModernUO's <c>Mobile.NetState != null</c>: a character
    /// someone is playing must not be deleted out from under them. Selection counts, not just being in
    /// the world, so the window between selecting and entering is covered too.
    /// </summary>
    bool IsCharacterPlayed(Serial mobileId);

    void Remove(long sessionId);

    bool TryGet(long sessionId, out PlayerSession session);
}
