using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Sessions;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class PacketDispatchWorkItem : IGameLoopWorkItem
{
    private readonly ISessionService _sessions;
    private readonly long _sessionId;
    private readonly IPacket _packet;
    private readonly Action<GameSession, IPacket> _handler;

    public PacketDispatchWorkItem(
        ISessionService sessions, long sessionId, IPacket packet, Action<GameSession, IPacket> handler
    )
    {
        _sessions = sessions;
        _sessionId = sessionId;
        _packet = packet;
        _handler = handler;
    }

    public void Execute()
    {
        if (_sessions.TryGet(_sessionId, out var session) &&
            session.NetworkSession.State != NetworkSessionState.Disconnected &&
            session.NetworkSession.Client is { IsConnected: true })
        {
            _handler(session, _packet);
        }
    }
}
