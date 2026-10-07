using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The client changes the lock of a skill (0x3A, variable): the number of the skill and the lock, both as the
///     client sent them, not yet checked against the skills and the locks there are.
/// </summary>
[PacketHandler(0x3A, PacketSizing.Variable, MinimumLength = 6, Description = "Skill lock")]
public sealed class SkillLockPacket : BasePacket<SkillLockPacket>, IIncomingPacket<SkillLockPacket>
{
    private const int PacketLength = 6;

    public override int Length { get; } = PacketLength;

    public required int Skill { get; init; }

    public required byte Lock { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out SkillLockPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data) || data.Length < PacketLength)
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadUInt16BigEndian(out var skill) || !reader.TryReadByte(out var skillLock))
        {
            return false;
        }

        packet = new() { Skill = skill, Lock = skillLock };

        return true;
    }
}
