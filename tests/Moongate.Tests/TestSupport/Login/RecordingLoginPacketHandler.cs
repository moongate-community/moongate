using Moongate.Network.Packets.General;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Tests.TestSupport.Login;

internal sealed class RecordingLoginPacketHandler : ILoginPacketHandler<PingPacket>
{
    public Func<LoginSession, PingPacket, CancellationToken, ValueTask> OnHandle { get; set; } =
        (_, _, _) => ValueTask.CompletedTask;

    public ValueTask HandleAsync(LoginSession session, PingPacket packet, CancellationToken cancellationToken)
        => OnHandle(session, packet, cancellationToken);
}
