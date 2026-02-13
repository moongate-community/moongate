using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Skills;

/// <summary>
/// OpCode 0x3A - Skill Update Packet
/// Consolidates: SkillUpdatePacket (full list), SkillChangePacket (single skill), SendSkillRequestPacket (client request)
/// Handles bidirectional communication: Client→Server requests and Server→Client responses
/// </summary>
public class SkillUpdatePacket : BaseUoPacket
{
    /// <summary>Skill update subcommand type</summary>
    public enum SkillUpdateType : byte
    {
        SkillList = 0x02,       // Server→Client: Full skill list
        SkillChange = 0xDF,     // Server→Client: Single skill change
        SkillRequest = 0x00     // Client→Server: Request skill update
    }

    public SkillUpdateType UpdateType { get; set; }
    public List<SkillEntry> Skills { get; set; } = new();
    public SkillEntry? SingleSkill { get; set; }

    /// <summary>Constructor for Server→Client: Full skill list</summary>
    public SkillUpdatePacket(UOMobileEntity mobile) : this()
    {
        UpdateType = SkillUpdateType.SkillList;
        Skills = mobile.Skills;
    }

    /// <summary>Constructor for Server→Client: Single skill change</summary>
    public SkillUpdatePacket(SkillEntry skill) : this()
    {
        UpdateType = SkillUpdateType.SkillChange;
        SingleSkill = skill;
    }

    /// <summary>Default constructor</summary>
    public SkillUpdatePacket() : base(0x3A) { }

    protected override bool Read(SpanReader reader)
    {
        // Client→Server: Skill request
        // Structure: [OpCode][Length][SkillID][LockState] ... [0x0000]
        UpdateType = SkillUpdateType.SkillRequest;

        var length = reader.ReadUInt16();
        var count = length / 3;

        for (var i = 0; i < count; i++)
        {
            var skillId = reader.ReadUInt16();
            var lockState = reader.ReadByte();

            Skills.Add(
                new()
                {
                    Skill = SkillInfo.Table[skillId],
                    Lock = (UOSkillLock)lockState
                }
            );
        }

        return true;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        if (UpdateType == SkillUpdateType.SkillList)
        {
            // Server→Client: Full skill list (0x02)
            writer.Write((ushort)(6 + (Skills.Count * 9)));
            writer.Write((byte)0x02);

            for (var i = 0; i < Skills.Count; i++)
            {
                var skill = Skills[i];
                var v = skill.Value;
                var uv = Math.Clamp((int)(v * 10), 0, 0xffff);

                writer.Write((ushort)(skill.Skill.SkillID + 1));
                writer.Write((ushort)uv);
                writer.Write((ushort)skill.Base);
                writer.Write((byte)skill.Lock);
                writer.Write((ushort)skill.Cap);
            }
            writer.Write((ushort)0);
        }
        else if (UpdateType == SkillUpdateType.SkillChange && SingleSkill != null)
        {
            // Server→Client: Single skill change (0xDF)
            writer.Write((ushort)13);
            var v = SingleSkill.Value;
            var uv = Math.Clamp((int)(v * 10), 0, 0xffff);

            writer.Write((byte)0xDF);
            writer.Write((ushort)SingleSkill.Skill.SkillID);
            writer.Write((ushort)uv);
            writer.Write((ushort)SingleSkill.Base);
            writer.Write((byte)SingleSkill.Lock);
            writer.Write((ushort)SingleSkill.Cap);
        }

        return writer.ToArray();
    }
}
