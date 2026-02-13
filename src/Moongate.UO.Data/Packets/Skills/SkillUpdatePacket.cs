using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;

namespace Moongate.UO.Data.Packets.Skills;

public class SkillUpdatePacket : BaseUoPacket
{
    public List<SkillEntry> Skills { get; set; } = new();

    public SkillUpdatePacket(UOMobileEntity mobile) : this()
        => Skills = mobile.Skills;

    public SkillUpdatePacket() : base(0x3A) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(6 + 9 + Skills.Count);
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

        return writer.ToArray();
    }
}
