using Moongate.Network.Packets.General;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class DependentPacketHandler : IPacketHandler<PingPacket>
{
    public DependentPacketHandler(RecordingPacketHandler dependency)
    {
    }

    public void Handle(GameSession session, PingPacket packet)
    {
    }
}
