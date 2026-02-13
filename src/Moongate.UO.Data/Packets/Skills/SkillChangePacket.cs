using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Skills;

namespace Moongate.UO.Data.Packets.Skills;

public class SkillChangePacket : BaseUoPacket
{
    public SkillEntry Skill { get; set; }

    public SkillChangePacket(SkillEntry skill) : this()
        => Skill = skill;

    public SkillChangePacket() : base(0x3A) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)13);
        var v = Skill.Value;
        var uv = Math.Clamp((int)(v * 10), 0, 0xffff);

        writer.Write((byte)0xDF);
        writer.Write((ushort)Skill.Skill.SkillID);
        writer.Write((ushort)uv);
        writer.Write((ushort)Skill.Base);
        writer.Write((byte)Skill.Lock);
        writer.Write((ushort)Skill.Cap);

        return writer.ToArray();
    }
}
