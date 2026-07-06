using Moongate.Server.Data.Session;
using SquidStd.Network.Client;

namespace Moongate.Server.Interfaces;

/// <summary>Owns the live client sessions keyed by connection id.</summary>
public interface ISessionManager
{
    PlayerSession GetOrCreate(SquidStdTcpClient client);

    bool TryGet(long sessionId, out PlayerSession session);

    void Remove(long sessionId);

    int Count { get; }
}
