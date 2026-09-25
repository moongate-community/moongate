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
///     The character the Enhanced Client asks to create; ClassicUO sends packet 0xF8 instead. Only the layout of the
///     current Enhanced Client is read: 146 bytes, big-endian.
/// </summary>
/// <remarks>
///     The values are the raw choices of the client and must be validated before use: gender, race and skills are
///     cast from bytes and may not be defined values. When a profession is chosen, the client leaves stats and skills
///     at 0 and the server applies those of the profession.
/// </remarks>
[PacketHandler(0x8D, PacketSizing.Variable, MinimumLength = PacketLength, Description = "Create character (Enhanced Client)")]
public sealed class CreateCharacterEnhancedPacket
    : BasePacket<CreateCharacterEnhancedPacket>, IIncomingPacket<CreateCharacterEnhancedPacket>
{
    private const int PacketLength = 146;
    private const int NameLength = 30;
    private const int SkillCount = 4;

    public override int Length => PacketLength;

    /// <summary>
    ///     The character slot the client asks to fill.
    /// </summary>
    public required uint CharacterSlot { get; init; }

    public required string Name { get; init; }

    /// <summary>
    ///     The profession id; 0 is the "Advanced" choice, where the player picked stats and skills.
    /// </summary>
    public required byte Profession { get; init; }

    /// <summary>
    ///     The index of the chosen city in the list of the character list packet (0xA9).
    /// </summary>
    public required byte StartingCity { get; init; }

    public required GenderType Gender { get; init; }

    /// <summary>
    ///     The race. The client sends it one higher (1 for human); the parser removes the offset.
    /// </summary>
    public required RaceType Race { get; init; }

    public required byte Strength { get; init; }

    public required byte Dexterity { get; init; }

    public required byte Intelligence { get; init; }

    public required Hue SkinHue { get; init; }

    /// <summary>
    ///     The four skills chosen for the "Advanced" profession; all zero when a profession was chosen.
    /// </summary>
    public required IReadOnlyList<CharacterSkillChoice> Skills { get; init; }

    public required Hue HairHue { get; init; }

    /// <summary>
    ///     The item id of the hair style; 0 means no hair.
    /// </summary>
    public required ushort HairStyle { get; init; }

    public required Hue ShirtHue { get; init; }

    public required ushort ShirtStyle { get; init; }

    public required Hue FaceHue { get; init; }

    public required ushort FaceStyle { get; init; }

    public required Hue BeardHue { get; init; }

    /// <summary>
    ///     The item id of the beard style; 0 means no beard.
    /// </summary>
    public required ushort BeardStyle { get; init; }

    public static bool TryParse(
        ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out CreateCharacterEnhancedPacket? packet
    )
    {
        packet = null;

        if (!HasValidHeader(data) || data.Length != PacketLength)
        {
            return false;
        }

        // Opcode and length are already checked; 4 bytes of an unknown pattern follow.
        var reader = new PacketReader(data[7..]);
        var skills = new CharacterSkillChoice[SkillCount];

        if (!reader.TryReadUInt32BigEndian(out var characterSlot) ||
            !reader.TryReadFixedAscii(NameLength, out var name) ||
            !reader.TryReadBytes(NameLength, out _) ||
            !reader.TryReadByte(out var profession) ||
            !reader.TryReadByte(out var startingCity) ||
            !reader.TryReadByte(out var gender) ||
            !reader.TryReadByte(out var race) ||
            !reader.TryReadByte(out var strength) ||
            !reader.TryReadByte(out var dexterity) ||
            !reader.TryReadByte(out var intelligence) ||
            !reader.TryReadUInt16BigEndian(out var skinHue) ||
            !reader.TryReadBytes(8, out _))
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

        if (!reader.TryReadBytes(26, out _) ||
            !reader.TryReadUInt16BigEndian(out var hairHue) ||
            !reader.TryReadUInt16BigEndian(out var hairStyle) ||
            !reader.TryReadBytes(6, out _) ||
            !reader.TryReadUInt16BigEndian(out var shirtHue) ||
            !reader.TryReadUInt16BigEndian(out var shirtStyle) ||
            !reader.TryReadBytes(1, out _) ||
            !reader.TryReadUInt16BigEndian(out var faceHue) ||
            !reader.TryReadUInt16BigEndian(out var faceStyle) ||
            !reader.TryReadBytes(1, out _) ||
            !reader.TryReadUInt16BigEndian(out var beardHue) ||
            !reader.TryReadUInt16BigEndian(out var beardStyle))
        {
            return false;
        }

        packet = new()
        {
            CharacterSlot = characterSlot,
            Name = name!,
            Profession = profession,
            StartingCity = startingCity,
            Gender = (GenderType)gender,
            Race = (RaceType)(race > 0 ? race - 1 : race),
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            SkinHue = new(skinHue),
            Skills = skills,
            HairHue = new(hairHue),
            HairStyle = hairStyle,
            ShirtHue = new(shirtHue),
            ShirtStyle = shirtStyle,
            FaceHue = new(faceHue),
            FaceStyle = faceStyle,
            BeardHue = new(beardHue),
            BeardStyle = beardStyle
        };

        return true;
    }
}
