using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Stat lock info (0xBF sub-command 0x19): the up/down/lock state of the three stats, packed two bits
/// each into a single byte. 12 bytes fixed. Without it the client's status-gump arrows never learn the
/// server's state, so they reset on every login.
/// </summary>
[PacketDocumentation(PacketFamilyType.StatusSkills, Length = 12, SubCommand = 0x19)]
public readonly record struct StatLockInfoPacket(
    Serial Serial,
    StatLockType StrengthLock,
    StatLockType DexterityLock,
    StatLockType IntelligenceLock
) : IOutgoingPacket
{
    public const byte PacketId = 0xBF;

    private const ushort SubCommand = 0x19;
    private const ushort Length = 12;
    private const byte ExtendedStatsVersion = 2;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Length);
        writer.Write(SubCommand);
        writer.Write(ExtendedStatsVersion);
        writer.Write(Serial);
        writer.Write((byte)0); // unused
        writer.Write((byte)(((byte)StrengthLock << 4) | ((byte)DexterityLock << 2) | (byte)IntelligenceLock));
    }
}
