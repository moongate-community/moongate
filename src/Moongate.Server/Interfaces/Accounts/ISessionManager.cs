using Moongate.Server.Data.Session;
using SquidStd.Network.Client;

namespace Moongate.Server.Interfaces.Accounts;

/// <summary>Owns the live client sessions keyed by connection id.</summary>
public interface ISessionManager
{
    int Count { get; }
    PlayerSession GetOrCreate(SquidStdTcpClient client);

    void Remove(long sessionId);

    bool TryGet(long sessionId, out PlayerSession session);
}
