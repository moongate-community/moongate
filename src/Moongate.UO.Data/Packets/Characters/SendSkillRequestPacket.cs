using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class SendSkillRequestPacket : BaseUoPacket
{
    public List<SkillEntry> Skills { get; set; } = new();

    public SendSkillRequestPacket() : base(0x3A) { }

    protected override bool Read(SpanReader reader)
    {
        //BYTE[1] Command
        //BYTE[2] Length
        //BYTE[2] Skill ID Number
        //BYTE[1] Skill Lock State

        var lenght = reader.ReadUInt16();

        var count = lenght / 3;

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
}
