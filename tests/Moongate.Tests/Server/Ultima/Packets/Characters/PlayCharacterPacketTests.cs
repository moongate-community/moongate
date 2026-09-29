using System.Buffers.Binary;
using System.Text;
using DryIoc;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class PlayCharacterPacketTests
{
    [Fact]
    public void TryDecode_KnownFixture_ReadsNameFlagsLoginCountAndIndex()
    {
        Assert.True(PacketCodec.TryDecode<PlayCharacterPacket>(Fixture(), out var packet));

        Assert.Equal("Aria", packet.Name);
        Assert.Equal((ClientFlags)0x3F, packet.ClientFlags);
        Assert.Equal(7u, packet.LoginCount);
        Assert.Equal(2, packet.CharacterIndex);
    }

    [Fact]
    public void TryDecode_WrongOpcodeOrLength_ReturnsFalse()
    {
        var frame = Fixture();

        Assert.False(PacketCodec.TryDecode<PlayCharacterPacket>(frame.AsSpan(0, 72), out _));
        Assert.False(PacketCodec.TryDecode<PlayCharacterPacket>([.. frame, 0x00], out _));
        frame[0] = 0x5E;
        Assert.False(PacketCodec.TryDecode<PlayCharacterPacket>(frame, out _));
    }

    [Fact]
    public void TryDecode_NameThatIsNotAscii_ReturnsFalse()
    {
        var frame = Fixture();
        frame[5] = 0xE9;

        Assert.False(PacketCodec.TryDecode<PlayCharacterPacket>(frame, out _));
    }

    [Fact]
    public void RegisterIncomingPacket_AddsItAsAFixedPacketToTheServerRegistry()
    {
        using var container = new Container();
        container.RegisterIncomingPacket<PlayCharacterPacket>();
        PacketRegistryFactory.Register(container);

        var registry = container.Resolve<PacketRegistry>();

        Assert.True(registry.TryGetDescriptor(0x5D, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(73, descriptor.FixedLength);
    }

    /// <summary>
    ///     A 73-byte frame in the order the client sends it: opcode, 0xEDEDEDED, name, 2 unknown bytes, client flags,
    ///     4 unknown bytes, login count, 16 unknown bytes, character index and the client's IP address.
    /// </summary>
    private static byte[] Fixture()
    {
        var frame = new byte[73];
        frame[0] = 0x5D;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1), 0xEDEDEDED);
        Encoding.ASCII.GetBytes("Aria").CopyTo(frame, 5);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(37), 0x3F);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(45), 7);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(65), 2);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(69), 0x7F000001);

        return frame;
    }
}
