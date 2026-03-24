using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Network.Packets;

public sealed class RequestCharProfilePacketTests
{
    [Test]
    public void TryParse_WhenDisplayRequest_ShouldReadModeAndTarget()
    {
        var bytes = BuildDisplayRequest((uint)0x00000042);
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x00));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x00000042u));
                Assert.That(packet.CommandType, Is.Null);
                Assert.That(packet.IgnoredField, Is.Null);
                Assert.That(packet.CharacterCount, Is.Null);
                Assert.That(packet.ProfileText, Is.Null);
            }
        );
    }

    [Test]
    public void TryParse_WhenDisplayRequestIncludesEnhancedClientPadding_ShouldReadModeAndTarget()
    {
        var bytes = BuildDisplayRequest((uint)0x00000000, trailingPaddingBytes: 4);
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x00));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x00000000u));
                Assert.That(packet.CommandType, Is.Null);
                Assert.That(packet.IgnoredField, Is.Null);
                Assert.That(packet.CharacterCount, Is.Null);
                Assert.That(packet.ProfileText, Is.Null);
            }
        );
    }

    [Test]
    public void TryParse_WhenUpdateRequest_ShouldReadAllFields()
    {
        var bytes = BuildUpdateRequest(
            (uint)0x00000044,
            0x0001,
            "Hello world"
        );
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x01));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x00000044u));
                Assert.That(packet.CommandType, Is.EqualTo((ushort)0x0001));
                Assert.That(packet.IgnoredField, Is.Null);
                Assert.That(packet.CharacterCount, Is.EqualTo((ushort)11));
                Assert.That(packet.ProfileText, Is.EqualTo("Hello world"));
            }
        );
    }

    [Test]
    public void TryParse_WhenUpdateRequestUsesLegacyReservedField_ShouldReadAllFields()
    {
        var bytes = BuildUpdateRequestWithReservedField(
            (uint)0x00000046,
            0x0001,
            0x0000,
            "Hello world"
        );
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x01));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x00000046u));
                Assert.That(packet.CommandType, Is.EqualTo((ushort)0x0001));
                Assert.That(packet.IgnoredField, Is.EqualTo((ushort)0x0000));
                Assert.That(packet.CharacterCount, Is.EqualTo((ushort)11));
                Assert.That(packet.ProfileText, Is.EqualTo("Hello world"));
            }
        );
    }

    [Test]
    public void TryParse_WhenUpdateRequestIncludesUnicodeTerminator_ShouldReadAllFields()
    {
        var bytes = BuildUpdateRequest(
            (uint)0x00000047,
            0x0001,
            "Hello world",
            appendUnicodeTerminator: true
        );
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(packet.Mode, Is.EqualTo(0x01));
                Assert.That(packet.TargetId, Is.EqualTo((Serial)0x00000047u));
                Assert.That(packet.CommandType, Is.EqualTo((ushort)0x0001));
                Assert.That(packet.IgnoredField, Is.Null);
                Assert.That(packet.CharacterCount, Is.EqualTo((ushort)11));
                Assert.That(packet.ProfileText, Is.EqualTo("Hello world"));
            }
        );
    }

    [Test]
    public void TryParse_WhenUpdateRequestExceedsModernUoLimit_ShouldFail()
    {
        var oversizedText = new string('a', 512);
        var bytes = BuildUpdateRequest(
            (uint)0x00000045,
            0x0001,
            oversizedText
        );
        var packet = new RequestCharProfilePacket();

        var ok = packet.TryParse(bytes);

        Assert.That(ok, Is.False);
    }

    private static byte[] BuildDisplayRequest(uint targetId, int trailingPaddingBytes = 0)
    {
        var writer = new SpanWriter(32, true);
        writer.Write((byte)0xB8);
        writer.Write((ushort)0);
        writer.Write((byte)0x00);
        writer.Write(targetId);

        for (var i = 0; i < trailingPaddingBytes; i++)
        {
            writer.Write((byte)0x00);
        }

        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }

    private static byte[] BuildUpdateRequest(
        uint targetId,
        ushort commandType,
        string profileText,
        bool appendUnicodeTerminator = false
    )
    {
        var writer = new SpanWriter(512, true);
        writer.Write((byte)0xB8);
        writer.Write((ushort)0);
        writer.Write((byte)0x01);
        writer.Write(targetId);
        writer.Write(commandType);
        writer.Write((ushort)profileText.Length);
        writer.WriteBigUni(profileText);

        if (appendUnicodeTerminator)
        {
            writer.Write((ushort)0x0000);
        }

        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }

    private static byte[] BuildUpdateRequestWithReservedField(
        uint targetId,
        ushort commandType,
        ushort ignoredField,
        string profileText
    )
    {
        var writer = new SpanWriter(512, true);
        writer.Write((byte)0xB8);
        writer.Write((ushort)0);
        writer.Write((byte)0x01);
        writer.Write(targetId);
        writer.Write(commandType);
        writer.Write(ignoredField);
        writer.Write((ushort)profileText.Length);
        writer.WriteBigUni(profileText);
        writer.WritePacketLength();
        var bytes = writer.ToArray();
        writer.Dispose();

        return bytes;
    }
}
