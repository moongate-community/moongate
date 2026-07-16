using Moongate.Network.Attributes;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Character creation (0xF8): the new 106-byte creation packet sent by clients 7.0.16.0 and later.
/// This reads the wire fields and decodes gender/race; resolving profession, city and applying the
/// starting loadout is the handler's job. An unrecognized gender/race byte falls back to a male human.
/// </summary>
[PacketDocumentation(PacketFamilyType.Characters, Length = 106)]
public readonly record struct CharacterCreationPacket(
    int Slot,
    string Name,
    uint ClientFlags,
    byte ProfessionId,
    GenderType Gender,
    RaceType Race,
    byte Strength,
    byte Dexterity,
    byte Intelligence,
    IReadOnlyList<CharacterSkill> Skills,
    short SkinHue,
    short HairStyle,
    short HairHue,
    short FacialHairStyle,
    short FacialHairHue,
    short StartingCityIndex,
    short ShirtHue,
    short PantsHue
) : IIncomingPacket<CharacterCreationPacket>
{
    public static byte PacketId => 0xF8;

    public static CharacterCreationPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();  // packet id
        reader.ReadInt32(); // 0xEDEDEDED pattern
        reader.ReadInt32(); // 0xFFFFFFFF
        reader.ReadByte();  // 0x00

        var name = reader.ReadAscii(30);
        reader.ReadBytes(2); // unknown

        var clientFlags = reader.ReadUInt32();
        reader.ReadInt32(); // unknown
        reader.ReadInt32(); // reserved

        var professionId = reader.ReadByte();
        reader.ReadBytes(15); // unknown

        var (gender, race) = DecodeGenderRace(reader.ReadByte());
        var strength = reader.ReadByte();
        var dexterity = reader.ReadByte();
        var intelligence = reader.ReadByte();

        var skills = new CharacterSkill[]
        {
            new(reader.ReadByte(), reader.ReadByte()),
            new(reader.ReadByte(), reader.ReadByte()),
            new(reader.ReadByte(), reader.ReadByte()),
            new(reader.ReadByte(), reader.ReadByte())
        };

        var skinHue = reader.ReadInt16();
        var hairStyle = reader.ReadInt16();
        var hairHue = reader.ReadInt16();
        var facialHairStyle = reader.ReadInt16();
        var facialHairHue = reader.ReadInt16();

        var startingCityIndex = reader.ReadInt16();
        reader.ReadBytes(2); // unknown

        var slot = reader.ReadInt16();
        reader.ReadInt32(); // reserved

        var shirtHue = reader.ReadInt16();
        var pantsHue = reader.ReadInt16();

        return new(
            slot,
            name,
            clientFlags,
            professionId,
            gender,
            race,
            strength,
            dexterity,
            intelligence,
            skills,
            skinHue,
            hairStyle,
            hairHue,
            facialHairStyle,
            facialHairHue,
            startingCityIndex,
            shirtHue,
            pantsHue
        );
    }

    private static (GenderType Gender, RaceType Race) DecodeGenderRace(byte value)
        => value switch
        {
            0 or 2 => (GenderType.Male, RaceType.Human),
            1 or 3 => (GenderType.Female, RaceType.Human),
            4      => (GenderType.Male, RaceType.Elf),
            5      => (GenderType.Female, RaceType.Elf),
            6      => (GenderType.Male, RaceType.Gargoyle),
            7      => (GenderType.Female, RaceType.Gargoyle),
            _      => (GenderType.Male, RaceType.Human)
        };
}
