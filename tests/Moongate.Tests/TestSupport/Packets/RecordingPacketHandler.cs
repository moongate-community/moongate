using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class RecordingPacketHandler : IPacketHandler<PingPacket>, IPacketHandler<ClientVersionPacket>
{
    public Action<GameSession, byte> OnPing { get; set; } = (_, _) => { };
    public string? Version { get; private set; }

    public void Handle(GameSession session, PingPacket packet)
    {
        OnPing(session, packet.Sequence);
    }

    public void Handle(GameSession session, ClientVersionPacket packet)
    {
        Version = packet.Version;
    }
}
