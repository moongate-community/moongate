using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// War mode (0x72): toggles the client's combat stance. 5 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.StatusSkills, Length = 5)]
public readonly record struct WarModePacket(bool WarMode) : IOutgoingPacket
{
    public const byte PacketId = 0x72;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((byte)(WarMode ? 1 : 0));
        writer.Write((byte)0x00);
        writer.Write((byte)0x32);
        writer.Write((byte)0x00);
    }
}
