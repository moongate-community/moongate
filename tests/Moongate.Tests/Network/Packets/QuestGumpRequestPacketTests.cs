using Moongate.Network.Packets.Incoming;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public class QuestGumpRequestPacketTests
{
    [Test]
    public void TryParse_WhenQuestButtonPacketIsValid_ShouldPopulatePacketFields()
    {
        var packet = new QuestGumpRequestPacket();

        var parsed = packet.TryParse(new byte[] { 0xD7, 0x00, 0x0A, 0x00, 0x00, 0x50, 0x01, 0x00, 0x32, 0x07 });

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.PlayerSerial, Is.EqualTo((Serial)0x00005001u));
                Assert.That(packet.EncodedCommandId, Is.EqualTo((ushort)0x0032));
                Assert.That(packet.EncodedCommandData.ToArray(), Is.EqualTo(new byte[] { 0x07 }));
            }
        );
    }

    [Test]
    public void TryParse_WhenEncodedCommandIsAnotherFamilyMember_ShouldStillParseEnvelope()
    {
        var packet = new QuestGumpRequestPacket();

        var parsed = packet.TryParse(new byte[] { 0xD7, 0x00, 0x09, 0x00, 0x00, 0x50, 0x01, 0x00, 0x28 });

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.PlayerSerial, Is.EqualTo((Serial)0x00005001u));
                Assert.That(packet.EncodedCommandId, Is.EqualTo((ushort)0x0028));
                Assert.That(packet.EncodedCommandData.IsEmpty, Is.True);
            }
        );
    }
}
