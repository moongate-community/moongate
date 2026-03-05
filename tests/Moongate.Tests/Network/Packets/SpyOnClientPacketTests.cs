using System.Buffers.Binary;
using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Spans;

namespace Moongate.Tests.Network.Packets;

public sealed class SpyOnClientPacketTests
{
    [Test]
    public void TryParse_ShouldReadHardwarePayload()
    {
        var packet = new SpyOnClientPacket();
        var payload = BuildPacketPayload();

        var parsed = packet.TryParse(payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(packet.ClientInfoVersion, Is.EqualTo(0x02));
                Assert.That(packet.InstanceId, Is.EqualTo(0x11223344u));
                Assert.That(packet.OsMajor, Is.EqualTo(10u));
                Assert.That(packet.OsMinor, Is.EqualTo(0u));
                Assert.That(packet.OsRevision, Is.EqualTo(19045u));
                Assert.That(packet.CpuManufacturer, Is.EqualTo(1));
                Assert.That(packet.CpuFamily, Is.EqualTo(6u));
                Assert.That(packet.CpuModel, Is.EqualTo(158u));
                Assert.That(packet.CpuClockSpeed, Is.EqualTo(3600u));
                Assert.That(packet.CpuQuantity, Is.EqualTo(8));
                Assert.That(packet.PhysicalMemory, Is.EqualTo(32768u));
                Assert.That(packet.ScreenWidth, Is.EqualTo(2560u));
                Assert.That(packet.ScreenHeight, Is.EqualTo(1440u));
                Assert.That(packet.ScreenDepth, Is.EqualTo(32u));
                Assert.That(packet.DirectXVersion, Is.EqualTo(12));
                Assert.That(packet.DirectXMinor, Is.EqualTo(0));
                Assert.That(packet.VideoCardDescription, Is.EqualTo("NVIDIA RTX"));
                Assert.That(packet.VideoCardVendorId, Is.EqualTo(0x10DEu));
                Assert.That(packet.VideoCardDeviceId, Is.EqualTo(0x2484u));
                Assert.That(packet.VideoCardMemory, Is.EqualTo(12288u));
                Assert.That(packet.Distribution, Is.EqualTo(1));
                Assert.That(packet.ClientsRunning, Is.EqualTo(1));
                Assert.That(packet.ClientsInstalled, Is.EqualTo(1));
                Assert.That(packet.PartialInstalled, Is.EqualTo(0));
                Assert.That(packet.UnknownFlag, Is.EqualTo(0));
                Assert.That(packet.LanguageCode, Is.EqualTo("ENU"));
                Assert.That(packet.UnknownEnding, Is.EqualTo("tail"));
            }
        );
    }

    [Test]
    public void TryParse_ShouldReturnFalse_WhenDeclaredLengthDoesNotMatch()
    {
        var packet = new SpyOnClientPacket();
        var payload = BuildPacketPayload();

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), 0xFFFF);
        var parsed = packet.TryParse(payload);

        Assert.That(parsed, Is.False);
    }

    private static byte[] BuildPacketPayload()
    {
        var writer = new SpanWriter(300, true);

        writer.Write((byte)0xD9);
        writer.Write((ushort)0); // placeholder length
        writer.Write((byte)0x02);
        writer.Write(0x11223344u);
        writer.Write(10u);
        writer.Write(0u);
        writer.Write(19045u);
        writer.Write((byte)1);
        writer.Write(6u);
        writer.Write(158u);
        writer.Write(3600u);
        writer.Write((byte)8);
        writer.Write(32768u);
        writer.Write(2560u);
        writer.Write(1440u);
        writer.Write(32u);
        writer.Write((ushort)12);
        writer.Write((ushort)0);
        writer.WriteLittleUni("NVIDIA RTX", 64);
        writer.Write(0x10DEu);
        writer.Write(0x2484u);
        writer.Write(12288u);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.WriteLittleUni("ENU", 4);
        writer.WriteAscii("tail", 64);

        var payload = writer.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(1, 2), (ushort)payload.Length);
        writer.Dispose();

        return payload;
    }
}
