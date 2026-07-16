using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Skill lock change (0x3A): the client sets the up/down/lock arrow on one skill. Variable length:
/// 3-byte header + skill id (ushort) + lock (byte). A lock value the client should not have sent is
/// clamped to <see cref="SkillLockType.Up" />. Shares its opcode with the outgoing skill list.
/// </summary>
[PacketDocumentation(PacketFamilyType.StatusSkills, IsVariableLength = true)]
public readonly record struct SkillLockChangePacket(ushort SkillId, SkillLockType Lock)
    : IIncomingPacket<SkillLockChangePacket>
{
    public static byte PacketId => 0x3A;

    public static SkillLockChangePacket Read(ref SpanReader reader)
    {
        reader.ReadByte();   // packet id
        reader.ReadUInt16(); // length
        var skillId = reader.ReadUInt16();
        var lockValue = reader.ReadByte();

        var skillLock = lockValue > (byte)SkillLockType.Locked ? SkillLockType.Up : (SkillLockType)lockValue;

        return new(skillId, skillLock);
    }
}
