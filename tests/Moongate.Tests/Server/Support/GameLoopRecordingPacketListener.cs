using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Session;

namespace Moongate.Tests.Server.Support;

public sealed class GameLoopRecordingPacketListener : IPacketListener
{
    public List<int> Sequences { get; } = [];

    public Task<bool> HandlePacketAsync(IGameSession session, IGameNetworkPacket packet)
    {
        if (packet is GameLoopTestPacket testPacket)
        {
            Sequences.Add(testPacket.Sequence);
        }

        return Task.FromResult(true);
    }
}
