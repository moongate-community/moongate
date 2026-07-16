using Moongate.Network.Attributes;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Skill list (0x3A), in the absolute-with-caps form (type 0x02): the client's whole skill list in one
/// go. Variable length: <c>6 + 9*Skills.Count</c>. Skills the mobile never trained are still sent, at
/// zero, otherwise they go missing from the client's list.
/// </summary>
[PacketDocumentation(PacketFamilyType.StatusSkills, IsVariableLength = true)]
public readonly record struct SkillsPacket(IReadOnlyList<SkillEntry> Skills) : IOutgoingPacket
{
    public const byte PacketId = 0x3A;

    private const byte AbsoluteWithCaps = 0x02;
    private const int HeaderLength = 4; // packet id + length + type
    private const int EntryLength = 9;
    private const int TerminatorLength = 2;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + EntryLength * Skills.Count + TerminatorLength));
        writer.Write(AbsoluteWithCaps);

        foreach (var skill in Skills)
        {
            writer.Write((ushort)(skill.SkillId + 1)); // the wire id is one-based
            writer.Write(skill.Value);
            writer.Write(skill.Base);
            writer.Write((byte)skill.Lock);
            writer.Write(skill.Cap);
        }

        writer.Write((ushort)0); // terminator
    }
}
