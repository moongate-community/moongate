using System.Buffers.Binary;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class ChangeCombatantPacketTests
{
    [Test]
    public void Write_ShouldSerializeExpectedPayload()
    {
        var packet = new ChangeCombatantPacket((Serial)0x00001000u);

        var data = Write(packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(data.Length, Is.EqualTo(5));
                Assert.That(data[0], Is.EqualTo(0xAA));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4)), Is.EqualTo(0x00001000u));
            }
        );
    }

    private static byte[] Write(IGameNetworkPacket packet)
    {
        var writer = new SpanWriter(16, true);
        packet.Write(ref writer);
        var data = writer.ToArray();
        writer.Dispose();

        return data;
    }
}
