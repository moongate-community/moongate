using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class CreateCharacterEnhancedPacketTests
{
    [Fact]
    public void TryDecode_KnownFixture_ReadsEveryField()
    {
        Assert.True(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(Fixture(), out var packet));

        Assert.Equal(0x8D, packet.OpCode);
        Assert.Equal(146, packet.Length);
        Assert.Equal(2u, packet.CharacterSlot);
        Assert.Equal("Aria", packet.Name);
        Assert.Equal(6, packet.Profession);
        Assert.Equal(3, packet.StartingCity);
        Assert.Equal(GenderType.Female, packet.Gender);
        Assert.Equal(RaceType.Elf, packet.Race);
        Assert.Equal((byte)40, packet.Strength);
        Assert.Equal((byte)30, packet.Dexterity);
        Assert.Equal((byte)20, packet.Intelligence);
        Assert.Equal(new Hue(0x83EA), packet.SkinHue);
        Assert.True(packet.SkinHue.IsPartial);
        Assert.Equal(
            [
                new CharacterSkillChoice { Skill = SkillType.Bushido, Value = 30 },
                new() { Skill = SkillType.Swordsmanship, Value = 30 },
                new() { Skill = SkillType.Focus, Value = 30 },
                new() { Skill = SkillType.Parrying, Value = 30 }
            ],
            packet.Skills
        );
        Assert.Equal(new Hue(0x044E), packet.HairHue);
        Assert.Equal(0x2FC1, packet.HairStyle);
        Assert.Equal(new Hue(0x0101), packet.ShirtHue);
        Assert.Equal(0x1517, packet.ShirtStyle);
        Assert.Equal(new Hue(0x0202), packet.FaceHue);
        Assert.Equal(0x3B44, packet.FaceStyle);
        Assert.Equal(new Hue(0x0303), packet.BeardHue);
        Assert.Equal(0x0000, packet.BeardStyle);
    }

    [Theory,
     InlineData(1, RaceType.Human),
     InlineData(2, RaceType.Elf),
     InlineData(3, RaceType.Gargoyle),
     InlineData(0, RaceType.Human)]
    public void TryDecode_Race_RemovesTheOffsetTheClientAdds(byte sent, RaceType expected)
    {
        var frame = Fixture();
        frame[74] = sent;

        Assert.True(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(frame, out var packet));
        Assert.Equal(expected, packet.Race);
    }

    [Fact]
    public void TryDecode_WrongOpcodeLengthOrDeclaredLength_ReturnsFalse()
    {
        var frame = Fixture();

        for (var length = 0; length < frame.Length; length++)
        {
            Assert.False(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(frame.AsSpan(0, length), out _));
        }

        var wrongOpcode = Fixture();
        wrongOpcode[0] = 0x8C;
        Assert.False(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(wrongOpcode, out _));

        var longer = new byte[147];
        frame.CopyTo(longer, 0);
        BinaryPrimitives.WriteUInt16BigEndian(longer.AsSpan(1), 147);
        Assert.False(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(longer, out _));

        var wrongDeclared = Fixture();
        BinaryPrimitives.WriteUInt16BigEndian(wrongDeclared.AsSpan(1), 145);
        Assert.False(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(wrongDeclared, out _));
    }

    [Fact]
    public void TryDecode_NameThatIsNotAscii_ReturnsFalse()
    {
        var frame = Fixture();
        frame[11] = 0xE9;

        Assert.False(PacketCodec.TryDecode<CreateCharacterEnhancedPacket>(frame, out _));
    }

    /// <summary>
    ///     A 146-byte frame built field by field at the offsets of the Enhanced Client layout.
    /// </summary>
    private static byte[] Fixture()
    {
        var frame = new byte[146];
        frame[0] = 0x8D;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(1), 146);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(3), 0xEDEDEDED);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(7), 2);
        Encoding.ASCII.GetBytes("Aria").CopyTo(frame, 11);
        Encoding.ASCII.GetBytes("Unknown").CopyTo(frame, 41);
        frame[71] = 6;
        frame[72] = 3;
        frame[73] = 1;
        frame[74] = 2;
        frame[75] = 40;
        frame[76] = 30;
        frame[77] = 20;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(78), 0x83EA);
        byte[] skills = [52, 30, 40, 30, 50, 30, 5, 30];
        skills.CopyTo(frame, 88);
        frame[121] = 0x0B;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(122), 0x044E);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(124), 0x2FC1);
        frame[126] = 0x0C;
        frame[131] = 0x0D;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(132), 0x0101);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(134), 0x1517);
        frame[136] = 0x0F;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(137), 0x0202);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(139), 0x3B44);
        frame[141] = 0x10;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(142), 0x0303);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(144), 0x0000);

        return frame;
    }
}
