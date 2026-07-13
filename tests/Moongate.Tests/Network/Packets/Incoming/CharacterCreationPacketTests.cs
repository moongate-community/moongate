using Moongate.Network.Packets.Incoming;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Incoming;

public class CharacterCreationPacketTests
{
    [Fact]
    public void Read_ParsesAllWireFields()
    {
        // Build the 106-byte 0xF8 payload in the exact order Read consumes it.
        var writer = new SpanWriter(stackalloc byte[106]);
        writer.Write((byte)0xF8);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.Write(unchecked((int)0xFFFFFFFF));
        writer.Write((byte)0x00);
        writer.WriteAscii("Tanngrisnir", 30);
        writer.Write((ushort)0);               // unknown (2 bytes)
        writer.Write(0x001Fu);                 // client flags
        writer.Write(0);                       // unknown
        writer.Write(0);                       // reserved
        writer.Write((byte)5);                 // profession id
        writer.Write(new byte[15]);            // unknown
        writer.Write((byte)3);                 // gender + race (female elf)
        writer.Write((byte)45);                // strength
        writer.Write((byte)25);                // dexterity
        writer.Write((byte)10);                // intelligence
        writer.Write((byte)7);
        writer.Write((byte)50);                // skill 1 (id, value)
        writer.Write((byte)8);
        writer.Write((byte)45);                // skill 2
        writer.Write((byte)2);
        writer.Write((byte)5);                 // skill 3
        writer.Write((byte)0);
        writer.Write((byte)0);                 // skill 4
        writer.Write((short)0x03E9);           // skin hue
        writer.Write((short)0x203B);           // hair style
        writer.Write((short)0x044E);           // hair hue
        writer.Write((short)0x203F);           // facial hair style
        writer.Write((short)0x044F);           // facial hair hue
        writer.Write((short)2);                // starting city index
        writer.Write((ushort)0);               // unknown (2 bytes)
        writer.Write((short)4);                // slot
        writer.Write(0);                       // reserved
        writer.Write((short)0x0765);           // shirt hue
        writer.Write((short)0x0766);           // pants hue

        var reader = new SpanReader(writer.Span.ToArray());
        var packet = CharacterCreationPacket.Read(ref reader);

        Assert.Equal((byte)0xF8, CharacterCreationPacket.PacketId);
        Assert.Equal(4, packet.Slot);
        Assert.Equal("Tanngrisnir", packet.Name);
        Assert.Equal(0x001Fu, packet.ClientFlags);
        Assert.Equal((byte)5, packet.ProfessionId);
        Assert.Equal(GenderType.Female, packet.Gender); // gender/race byte 3 = female human
        Assert.Equal(RaceType.Human, packet.Race);
        Assert.Equal((byte)45, packet.Strength);
        Assert.Equal((byte)25, packet.Dexterity);
        Assert.Equal((byte)10, packet.Intelligence);
        Assert.Equal(4, packet.Skills.Count);
        Assert.Equal((byte)7, packet.Skills[0].SkillId);
        Assert.Equal((byte)50, packet.Skills[0].Value);
        Assert.Equal((byte)2, packet.Skills[2].SkillId);
        Assert.Equal((byte)5, packet.Skills[2].Value);
        Assert.Equal((short)0x03E9, packet.SkinHue);
        Assert.Equal((short)0x203B, packet.HairStyle);
        Assert.Equal((short)0x044E, packet.HairHue);
        Assert.Equal((short)0x203F, packet.FacialHairStyle);
        Assert.Equal((short)0x044F, packet.FacialHairHue);
        Assert.Equal((short)2, packet.StartingCityIndex);
        Assert.Equal((short)0x0765, packet.ShirtHue);
        Assert.Equal((short)0x0766, packet.PantsHue);
    }
}
