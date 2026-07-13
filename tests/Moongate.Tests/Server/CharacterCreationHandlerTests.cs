using Moongate.Network.Data;
using Moongate.Network.Packets.Incoming;
using Moongate.Server.Handlers;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class CharacterCreationHandlerTests
{
    [Fact]
    public void CreateCharacter_MapsIdentityStatsAndAppearanceHues()
    {
        var packet = new CharacterCreationPacket(
            Slot: 0,
            Name: "Freydis",
            ClientFlags: 0,
            ProfessionId: 4,
            Gender: GenderType.Female,
            Race: RaceType.Elf,
            Strength: 45,
            Dexterity: 20,
            Intelligence: 25,
            Skills: [new CharacterSkill(1, 50), new CharacterSkill(2, 30), new CharacterSkill(3, 20), new CharacterSkill(0, 0)],
            SkinHue: 0x03EA,
            HairStyle: 0x203C,
            HairHue: 0x044E,
            FacialHairStyle: 0x2040,
            FacialHairHue: 0x0450,
            StartingCityIndex: 0,
            ShirtHue: 0x0765,
            PantsHue: 0x0766
        );

        var character = CharacterCreationHandler.CreateCharacter(packet);

        Assert.Equal("Freydis", character.Name);
        Assert.Equal(GenderType.Female, character.Gender);
        Assert.Equal(RaceType.Elf, character.Race);
        Assert.Equal((byte)4, character.ProfessionId);
        Assert.Equal(45, character.Strength);
        Assert.Equal(20, character.Dexterity);
        Assert.Equal(25, character.Intelligence);
        Assert.Equal((ushort)0x03EA, character.SkinHue.Value);
        Assert.Equal((ushort)0x203C, character.HairStyle);
        Assert.Equal((ushort)0x044E, character.HairHue.Value);
        Assert.Equal((ushort)0x2040, character.FacialHairStyle);
        Assert.Equal((ushort)0x0450, character.FacialHairHue.Value);

        // Four skill slots: three chosen (stored in tenths) and one unused (0,0) skipped.
        Assert.Equal(3, character.Skills.Count);
        Assert.Equal(500, character.Skills[1]);
        Assert.Equal(300, character.Skills[2]);
        Assert.Equal(200, character.Skills[3]);
        Assert.False(character.Skills.ContainsKey(0));
    }
}
