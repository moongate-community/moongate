using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class CharacterCreationPacket : BaseUoPacket
{
    public int Slot { get; set; }
    public string CharacterName { get; set; }
    public ClientFlags ClientFlags { get; set; }
    public ProfessionInfo Profession { get; set; }
    public List<SkillKeyValue> Skills { get; set; }

    public int Intelligence { get; set; }

    public int Strength { get; set; }

    public int Dexterity { get; set; }

    public GenderType Gender { get; set; }

    public HueStyle Hair { get; set; }
    public HueStyle FacialHair { get; set; }
    public CityInfo StartingCity { get; set; }
    public Race Race { get; set; }

    public HueStyle Shirt { get; set; }

    public HueStyle Skin { get; set; }
    public HueStyle Pants { get; set; }

    public CharacterCreationPacket() : base(0xF8)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        reader.ReadInt32(); // 0xedededed
        reader.ReadInt32(); // 0xffffffff
        reader.ReadByte();  // 0x00
        CharacterName = reader.ReadAscii(30);
        reader.ReadBytes(2);

        ClientFlags = (ClientFlags)reader.ReadUInt32();
        reader.ReadInt32();
        reader.ReadInt32(); // 0x00000000

        var professionByte = reader.ReadByte();
        ProfessionInfo.GetProfession(professionByte, out var profession);

        Profession = profession;

        reader.ReadBytes(15);

        var genderAndRace = ParseGenderByte(reader.ReadByte());

        Gender = genderAndRace.gender;
        Race = genderAndRace.race;

        Strength = reader.ReadByte();
        Dexterity = reader.ReadByte();
        Intelligence = reader.ReadByte();

        var skill1 = new SkillKeyValue((SkillName)reader.ReadByte(), reader.ReadByte());
        var skill2 = new SkillKeyValue((SkillName)reader.ReadByte(), reader.ReadByte());
        var skill3 = new SkillKeyValue((SkillName)reader.ReadByte(), reader.ReadByte());
        var skill4 = new SkillKeyValue((SkillName)reader.ReadByte(), reader.ReadByte());

        Skills = [skill1, skill2, skill3, skill4];

        Skin = new HueStyle(0x00, reader.ReadInt16());

        Hair = new HueStyle(reader.ReadInt16(), reader.ReadInt16());
        FacialHair = new HueStyle(reader.ReadInt16(), reader.ReadInt16());

        StartingCity = StartingCities.AvailableStartingCities[reader.ReadInt16()];
        reader.ReadBytes(2);
        Slot = reader.ReadInt16();

        reader.ReadInt32();

        Shirt = new HueStyle(0x00, reader.ReadInt16());
        Pants = new HueStyle(0x00, reader.ReadInt16());

        return true;
    }

    private static (GenderType gender, Race race) ParseGenderByte(byte value)
    {
        return value switch
        {
            0 or 2 => (GenderType.Male, Race.Human),
            1 or 3 => (GenderType.Female, Race.Human),
            4      => (GenderType.Male, Race.Elf),
            5      => (GenderType.Female, Race.Elf),
            6      => (GenderType.Male, Race.Gargoyle),
            7      => (GenderType.Female, Race.Gargoyle),
            _      => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid Sex byte: {value}")
        };
    }
}

public record SkillKeyValue(SkillName Skill, int Value);

public record HueStyle(short Style, short Hue);
