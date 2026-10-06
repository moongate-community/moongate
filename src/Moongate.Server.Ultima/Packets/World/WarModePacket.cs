using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Puts the player in peace or war mode (0x72).
/// </summary>
[PacketHandler(0x72, PacketSizing.Fixed, Length = 5)]
public sealed class WarModePacket : BaseFixedPacket<WarModePacket>, IOutgoingPacket
{
    // The byte the client expects after the war mode, as ModernUO sends it.
    private const byte TrailingMarker = 0x32;

    public bool WarMode { get; }

    public WarModePacket(bool warMode)
    {
        WarMode = warMode;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(WarMode ? (byte)1 : (byte)0);

        // Fixed trailing bytes the client expects.
        writer.WriteByte(0x00);
        writer.WriteByte(TrailingMarker);
        writer.WriteByte(0x00);
    }
}
