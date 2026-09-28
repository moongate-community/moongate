using System.Buffers.Binary;
using System.Text;
using DryIoc;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Ultima.Packets.Characters;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class DeleteCharacterPacketTests
{
    [Fact]
    public void TryDecode_KnownFixture_ReadsTheIndex_AndIgnoresThePassword()
    {
        Assert.True(PacketCodec.TryDecode<DeleteCharacterPacket>(Fixture(3), out var packet));

        Assert.Equal(3, packet.CharacterIndex);
    }

    [Fact]
    public void TryDecode_WrongOpcodeOrLength_ReturnsFalse()
    {
        var frame = Fixture(0);

        Assert.False(PacketCodec.TryDecode<DeleteCharacterPacket>(frame.AsSpan(0, 38), out _));
        Assert.False(PacketCodec.TryDecode<DeleteCharacterPacket>([.. frame, 0x00], out _));
        frame[0] = 0x84;
        Assert.False(PacketCodec.TryDecode<DeleteCharacterPacket>(frame, out _));
    }

    [Fact]
    public void RegisterIncomingPacket_AddsItAsAFixedPacketToTheServerRegistry()
    {
        using var container = new Container();
        container.RegisterIncomingPacket<DeleteCharacterPacket>();
        PacketRegistryFactory.Register(container);

        var registry = container.Resolve<PacketRegistry>();

        Assert.True(registry.TryGetDescriptor(0x83, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(39, descriptor.FixedLength);
    }

    /// <summary>
    ///     Opcode, a 30-byte password, the character's index in the list and the client's IP address.
    /// </summary>
    private static byte[] Fixture(int index)
    {
        var frame = new byte[39];
        frame[0] = 0x83;
        Encoding.ASCII.GetBytes("secret").CopyTo(frame, 1);
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(31), index);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(35), 0x7F000001);

        return frame;
    }
}
