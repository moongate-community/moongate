using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The skills of the player's own character (0x3A), with their caps: the whole list (type 0x02, the skills
///     numbered from 1 and a closing zero) or one skill that changed (type 0xDF, numbered from 0). Each is its value,
///     its base, its lock and its cap, in tenths of a point.
/// </summary>
[PacketHandler(0x3A, PacketSizing.Variable, MinimumLength = 6)]
public sealed class SkillsPacket : BasePacket<SkillsPacket>, IOutgoingPacket
{
    private const byte AllCapped = 0x02;
    private const byte OneCapped = 0xDF;
    private const int SkillLength = 9;

    private readonly bool _all;

    public override int Length => _all ? 6 + SkillLength * Skills.Count : 4 + SkillLength;

    public IReadOnlyList<MobileSkill> Skills { get; }

    private SkillsPacket(IReadOnlyList<MobileSkill> skills, bool all)
    {
        Skills = skills;
        _all = all;
    }

    /// <summary>
    ///     The whole list, as the client asks it when the skill window opens.
    /// </summary>
    public static SkillsPacket All(IReadOnlyList<MobileSkill> skills)
    {
        return new(skills, true);
    }

    /// <summary>
    ///     One skill whose value, lock or cap changed.
    /// </summary>
    public static SkillsPacket One(MobileSkill skill)
    {
        return new([skill], false);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte(_all ? AllCapped : OneCapped);

        foreach (var skill in Skills)
        {
            writer.WriteUInt16BigEndian((ushort)((int)skill.Skill + (_all ? 1 : 0)));
            writer.WriteUInt16BigEndian((ushort)skill.Base);
            writer.WriteUInt16BigEndian((ushort)skill.Base);
            writer.WriteByte((byte)skill.Lock);
            writer.WriteUInt16BigEndian((ushort)skill.Cap);
        }

        if (_all)
        {
            writer.WriteUInt16BigEndian(0);
        }
    }
}
