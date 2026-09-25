using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.General;

public class ClientHardwareInfoPacketTests
{
    private const int VideoDescriptionOffset = 52;
    private const int VideoDescriptionLength = 128;

    private static readonly byte[] Fixture = Convert.FromHexString(
        "D9" + "02" +
        "01020304" +
        "0000000A" + "00000000" + "00004A65" +
        "01" +
        "00000006" + "0000009E" + "00000E10" +
        "08" +
        "00004000" +
        "00000780" + "00000438" + "00000020" +
        "000C" + "0000" +
        "0047" + "0065" + "0046" + "006F" + "0072" + "0063" + "0065" + Zeros(114) +
        "000010DE" + "00002484" + "00002000" +
        "01" + "02" + "03" + "00" +
        "0045004E00550000" +
        Repeat("AB", 64)
    );

    [Fact]
    public void Registry_KnowsItAsAFixedIncomingPacket()
    {
        var registry = PacketTable.CreateRegistry();

        Assert.True(registry.TryGetDescriptor(0xD9, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(PacketSizing.Fixed, descriptor.Sizing);
        Assert.Equal(268, descriptor.FixedLength);
    }

    [Fact]
    public void TryDecode_FullWidthVideoDescription_ReadsAllCharacters()
    {
        var frame = Fixture.ToArray();
        Convert.FromHexString(Repeat("0041", VideoDescriptionLength / 2)).CopyTo(frame, VideoDescriptionOffset);

        Assert.True(PacketCodec.TryDecode<ClientHardwareInfoPacket>(frame, out var packet));
        Assert.Equal(new string('A', VideoDescriptionLength / 2), packet.VideoDescription);
    }

    [Fact]
    public void TryDecode_IncompleteWrongOrAppendedFrame_ReturnsFalse()
    {
        Assert.Equal(268, Fixture.Length);

        for (var length = 0; length < Fixture.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<ClientHardwareInfoPacket>(Fixture.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture.ToArray();
        wrongOpcode[0] = 0xD8;
        Assert.False(PacketCodec.TryDecode<ClientHardwareInfoPacket>(wrongOpcode, out _));
        Assert.False(PacketCodec.TryDecode<ClientHardwareInfoPacket>([.. Fixture, 0x00], out _));
    }

    [Fact]
    public void TryDecode_KnownFixture_ReadsEveryField()
    {
        Assert.True(PacketCodec.TryDecode<ClientHardwareInfoPacket>(Fixture, out var packet));

        Assert.Equal(0xD9, packet.OpCode);
        Assert.Equal(268, packet.Length);
        Assert.Equal(2, packet.ClientType);
        Assert.Equal(0x01020304u, packet.InstanceId);
        Assert.Equal(10u, packet.OsMajor);
        Assert.Equal(0u, packet.OsMinor);
        Assert.Equal(19045u, packet.OsRevision);
        Assert.Equal(1, packet.CpuManufacturer);
        Assert.Equal(6u, packet.CpuFamily);
        Assert.Equal(158u, packet.CpuModel);
        Assert.Equal(3600u, packet.CpuClockSpeedMhz);
        Assert.Equal(8, packet.CpuCount);
        Assert.Equal(16384u, packet.MemoryMb);
        Assert.Equal(1920u, packet.ScreenWidth);
        Assert.Equal(1080u, packet.ScreenHeight);
        Assert.Equal(32u, packet.ScreenDepth);
        Assert.Equal(12, packet.DirectXMajor);
        Assert.Equal(0, packet.DirectXMinor);
        Assert.Equal("GeForce", packet.VideoDescription);
        Assert.Equal(0x10DEu, packet.VideoVendorId);
        Assert.Equal(0x2484u, packet.VideoDeviceId);
        Assert.Equal(8192u, packet.VideoMemoryMb);
        Assert.Equal(1, packet.Distribution);
        Assert.Equal(2, packet.ClientsRunning);
        Assert.Equal(3, packet.ClientsInstalled);
        Assert.Equal(0, packet.PartialInstalled);
        Assert.Equal("ENU", packet.LanguageCode);
    }

    private static string Repeat(string hex, int count)
    {
        return string.Concat(Enumerable.Repeat(hex, count));
    }

    private static string Zeros(int byteCount)
    {
        return new('0', byteCount * 2);
    }
}
