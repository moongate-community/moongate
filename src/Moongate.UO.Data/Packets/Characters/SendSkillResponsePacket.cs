using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class SendSkillResponsePacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }
    public SendSkillResponseType ResponseType { get; set; }
    public int? SingleSkillId { get; set; } // For single skill updates

    public SendSkillResponsePacket() : base(0x3A) { }

    public SendSkillResponsePacket(
        UOMobileEntity mobile,
        SendSkillResponseType responseType,
        int? singleSkillId = null
    ) : this()
    {
        Mobile = mobile;
        ResponseType = responseType;
        SingleSkillId = singleSkillId;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        var lengthPos = writer.Position;
        writer.Write((ushort)0); // Placeholder for length

        writer.Write((byte)ResponseType);

        if (ResponseType == SendSkillResponseType.SingleSkillUpdate ||
            ResponseType == SendSkillResponseType.SingleSkillUpdateWithCap)
        {
            // Single skill update
            if (SingleSkillId.HasValue)
            {
                var skillEntry = Mobile.Skills.FirstOrDefault(s => s.Skill.SkillID == SingleSkillId.Value);

                if (skillEntry != null)
                {
                    WriteSkillEntry(writer, skillEntry);
                }
            }
        }
        else
        {
            // Full skill list
            foreach (var skillEntry in Mobile.Skills)
            {
                WriteSkillEntry(writer, skillEntry);
            }

            // Null terminator for full list (0x00 type only)
            if (ResponseType == SendSkillResponseType.FullSkillList)
            {
                writer.Write((ushort)0x0000);
            }
        }

        // Write packet length
        var currentPosition = writer.Position;
        writer.Seek(1, SeekOrigin.Begin);
        writer.Write((ushort)currentPosition);
        writer.Seek(currentPosition, SeekOrigin.Begin);

        return writer.ToArray();
    }

    private void WriteSkillEntry(SpanWriter writer, SkillEntry skillEntry)
    {
        writer.Write((ushort)skillEntry.Skill.SkillID);
        writer.Write((ushort)(skillEntry.Value * 10)); // Skill value * 10
        writer.Write((ushort)(skillEntry.Base * 10));  // Unmodified value * 10
        writer.Write((byte)skillEntry.Lock);

        // Write skill cap if required by response type
        if (ResponseType == SendSkillResponseType.FullSkillListWithCap ||
            ResponseType == SendSkillResponseType.SingleSkillUpdateWithCap)
        {
            writer.Write((ushort)skillEntry.Cap);
        }
    }
}
