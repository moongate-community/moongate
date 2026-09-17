using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class PluginServerSelectHandler : IPacketHandler<ServerSelectPacket>
{
    private readonly IPacketSendService _sender;
    private readonly IGameLoopService _loop;

    public PluginServerSelectHandler(PacketPluginDependency dependency, IPacketSendService sender, IGameLoopService loop)
    {
        Assert.True(dependency.Started);
        _sender = sender;
        _loop = loop;
    }

    public void Handle(GameSession session, ServerSelectPacket packet)
    {
        Assert.True(_loop.IsOnLoopThread);
        Assert.True(_sender.TrySend(session.SessionId, new PingPacket((byte)packet.ServerIndex)));
    }
}
