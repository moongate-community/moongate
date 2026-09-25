using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     The character the classic client (ClassicUO, 7.0.16 and later) asks to create; the Enhanced Client sends
///     packet 0x8D instead. 106 bytes, big-endian.
/// </summary>
/// <remarks>
///     The values are the raw choices of the client and must be validated before use: gender, race and skills are
///     read from bytes and may not be defined values. When a profession is chosen, the client leaves stats and skills
///     at 0 and the server applies those of the profession.
/// </remarks>
[PacketHandler(0xF8, PacketSizing.Fixed, Length = 106, Description = "Create character")]
public sealed class CreateCharacterPacket : BaseFixedPacket<CreateCharacterPacket>, IIncomingPacket<CreateCharacterPacket>
{
    private const int NameLength = 30;
    private const int SkillCount = 4;

    public required string Name { get; init; }

    /// <summary>
    ///     What the client says it can show, mostly the maps it has installed.
    /// </summary>
    public required ClientFlags ClientFlags { get; init; }

    /// <summary>
    ///     The profession id; 0 is the "Advanced" choice, where the player picked stats and skills.
    /// </summary>
    public required byte Profession { get; init; }

    public required GenderType Gender { get; init; }

    public required RaceType Race { get; init; }

    public required byte Strength { get; init; }

    public required byte Dexterity { get; init; }

    public required byte Intelligence { get; init; }

    /// <summary>
    ///     The four skills chosen for the "Advanced" profession; all zero when a profession was chosen.
    /// </summary>
    public required IReadOnlyList<CharacterSkillChoice> Skills { get; init; }

    public required Hue SkinHue { get; init; }

    /// <summary>
    ///     The item id of the hair style; 0 means no hair.
    /// </summary>
    public required ushort HairStyle { get; init; }

    public required Hue HairHue { get; init; }

    /// <summary>
    ///     The item id of the beard style; 0 means no beard.
    /// </summary>
    public required ushort BeardStyle { get; init; }

    public required Hue BeardHue { get; init; }

    /// <summary>
    ///     The index of the chosen city in the list of the character list packet (0xA9).
    /// </summary>
    public required ushort StartingCity { get; init; }

    /// <summary>
    ///     The character slot the client asks to fill.
    /// </summary>
    public required ushort CharacterSlot { get; init; }

    public required Hue ShirtHue { get; init; }

    public required Hue PantsHue { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out CreateCharacterPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        // Opcode, then 9 bytes of a fixed pattern (0xEDEDEDED, 0xFFFFFFFF, 0x00).
        var reader = new PacketReader(data[10..]);
        var skills = new CharacterSkillChoice[SkillCount];

        // After the name: 2 zero bytes, the client flags, 2 more integers, then 15 zero bytes after the profession.
        if (!reader.TryReadFixedAscii(NameLength, out var name) ||
            !reader.TryReadBytes(2, out _) ||
            !reader.TryReadUInt32BigEndian(out var clientFlags) ||
            !reader.TryReadBytes(8, out _) ||
            !reader.TryReadByte(out var profession) ||
            !reader.TryReadBytes(15, out _) ||
            !reader.TryReadByte(out var genderRace) ||
            !reader.TryReadByte(out var strength) ||
            !reader.TryReadByte(out var dexterity) ||
            !reader.TryReadByte(out var intelligence))
        {
            return false;
        }

        for (var index = 0; index < SkillCount; index++)
        {
            if (!reader.TryReadByte(out var skill) || !reader.TryReadByte(out var value))
            {
                return false;
            }

            skills[index] = new() { Skill = (SkillType)skill, Value = value };
        }

        // After the character slot come 4 bytes of the client's IP address.
        if (!reader.TryReadUInt16BigEndian(out var skinHue) ||
            !reader.TryReadUInt16BigEndian(out var hairStyle) ||
            !reader.TryReadUInt16BigEndian(out var hairHue) ||
            !reader.TryReadUInt16BigEndian(out var beardStyle) ||
            !reader.TryReadUInt16BigEndian(out var beardHue) ||
            !reader.TryReadUInt16BigEndian(out var startingCity) ||
            !reader.TryReadBytes(2, out _) ||
            !reader.TryReadUInt16BigEndian(out var characterSlot) ||
            !reader.TryReadBytes(4, out _) ||
            !reader.TryReadUInt16BigEndian(out var shirtHue) ||
            !reader.TryReadUInt16BigEndian(out var pantsHue))
        {
            return false;
        }

        packet = new()
        {
            Name = name!,
            ClientFlags = (ClientFlags)clientFlags,
            Profession = profession,
            Gender = (GenderType)(genderRace % 2),
            Race = ToRace(genderRace),
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            Skills = skills,
            SkinHue = new(skinHue),
            HairStyle = hairStyle,
            HairHue = new(hairHue),
            BeardStyle = beardStyle,
            BeardHue = new(beardHue),
            StartingCity = startingCity,
            CharacterSlot = characterSlot,
            ShirtHue = new(shirtHue),
            PantsHue = new(pantsHue)
        };

        return true;
    }

    /// <summary>
    ///     The client packs race and gender into one byte as <c>race * 2 + gender</c>, with the race counted from 1
    ///     (human 2 and 3, elf 4 and 5, gargoyle 6 and 7). Values below 4 are read as human.
    /// </summary>
    private static RaceType ToRace(byte genderRace)
    {
        return genderRace < 4 ? RaceType.Human : (RaceType)(genderRace / 2 - 1);
    }
}
