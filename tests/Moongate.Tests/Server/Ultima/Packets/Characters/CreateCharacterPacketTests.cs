using System.Buffers.Binary;
using System.Text;
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class CreateCharacterPacketTests
{
    [Fact]
    public void TryDecode_KnownFixture_ReadsEveryField()
    {
        Assert.True(PacketCodec.TryDecode<CreateCharacterPacket>(Fixture(), out var packet));

        Assert.Equal(0xF8, packet.OpCode);
        Assert.Equal(106, packet.Length);
        Assert.Equal("Aria", packet.Name);
        Assert.Equal(0, packet.Profession);
        Assert.Equal(GenderType.Female, packet.Gender);
        Assert.Equal(RaceType.Elf, packet.Race);
        Assert.Equal((byte)35, packet.Strength);
        Assert.Equal((byte)35, packet.Dexterity);
        Assert.Equal((byte)20, packet.Intelligence);
        Assert.Equal(
            [
                new CharacterSkillChoice { Skill = SkillType.Magery, Value = 50 },
                new() { Skill = SkillType.EvaluatingIntelligence, Value = 50 },
                new() { Skill = SkillType.Meditation, Value = 20 },
                new() { Skill = SkillType.Wrestling, Value = 0 }
            ],
            packet.Skills
        );
        Assert.Equal(new Hue(0x83EA), packet.SkinHue);
        Assert.Equal(0x2FC1, packet.HairStyle);
        Assert.Equal(new Hue(0x044E), packet.HairHue);
        Assert.Equal(0, packet.BeardStyle);
        Assert.Equal(Hue.None, packet.BeardHue);
        Assert.Equal(3, packet.StartingCity);
        Assert.Equal(2, packet.CharacterSlot);
        Assert.Equal(new Hue(0x0101), packet.ShirtHue);
        Assert.Equal(new Hue(0x0202), packet.PantsHue);
    }

    [Theory,
     InlineData(2, RaceType.Human, GenderType.Male),
     InlineData(3, RaceType.Human, GenderType.Female),
     InlineData(4, RaceType.Elf, GenderType.Male),
     InlineData(5, RaceType.Elf, GenderType.Female),
     InlineData(6, RaceType.Gargoyle, GenderType.Male),
     InlineData(7, RaceType.Gargoyle, GenderType.Female),
     InlineData(0, RaceType.Human, GenderType.Male),
     InlineData(1, RaceType.Human, GenderType.Female)]
    public void TryDecode_GenderRaceByte_SplitsRaceAndGender(byte sent, RaceType race, GenderType gender)
    {
        var frame = Fixture();
        frame[70] = sent;

        Assert.True(PacketCodec.TryDecode<CreateCharacterPacket>(frame, out var packet));
        Assert.Equal(race, packet.Race);
        Assert.Equal(gender, packet.Gender);
    }

    [Fact]
    public void TryDecode_WrongOpcodeOrLength_ReturnsFalse()
    {
        var frame = Fixture();

        for (var length = 0; length < frame.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<CreateCharacterPacket>(frame.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture();
        wrongOpcode[0] = 0x00;
        Assert.False(PacketCodec.TryDecode<CreateCharacterPacket>(wrongOpcode, out _));
        Assert.False(PacketCodec.TryDecode<CreateCharacterPacket>([.. frame, 0x00], out _));
    }

    [Fact]
    public void TryDecode_NameThatIsNotAscii_ReturnsFalse()
    {
        var frame = Fixture();
        frame[10] = 0xE9;

        Assert.False(PacketCodec.TryDecode<CreateCharacterPacket>(frame, out _));
    }

    [Fact]
    public void RegisterIncomingPacket_AddsItAsAFixedPacketToTheServerRegistry()
    {
        using var container = new Container();
        container.RegisterIncomingPacket<CreateCharacterPacket>();
        PacketRegistryFactory.Register(container);

        var registry = container.Resolve<PacketRegistry>();

        Assert.True(registry.TryGetDescriptor(0xF8, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(PacketSizing.Fixed, descriptor.Sizing);
        Assert.Equal(106, descriptor.FixedLength);
        Assert.True(registry.TryDecode(Fixture(), out var decoded));
        Assert.IsType<CreateCharacterPacket>(decoded);
    }

    /// <summary>
    ///     A 106-byte frame written field by field in the order ClassicUO sends it.
    /// </summary>
    private static byte[] Fixture()
    {
        var frame = new byte[106];
        frame[0] = 0xF8;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1), 0xEDEDEDED);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(5), 0xFFFFFFFF);
        frame[9] = 0x00;
        Encoding.ASCII.GetBytes("Aria").CopyTo(frame, 10);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(42), 0x000000FF);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(46), 0x01);
        frame[54] = 0;
        frame[70] = 5;
        frame[71] = 35;
        frame[72] = 35;
        frame[73] = 20;
        byte[] skills = [25, 50, 16, 50, 46, 20, 43, 0];
        skills.CopyTo(frame, 74);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(82), 0x83EA);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(84), 0x2FC1);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(86), 0x044E);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(92), 3);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(96), 2);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(98), 0x7F000001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(102), 0x0101);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(104), 0x0202);

        return frame;
    }
}
