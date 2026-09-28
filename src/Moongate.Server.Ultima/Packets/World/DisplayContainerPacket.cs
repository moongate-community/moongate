using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Opens the gump of a container (0x24): 7 bytes, or 9 with the container type for clients from 7.0.9.0.
/// </summary>
[PacketHandler(0x24, PacketSizing.Variable, MinimumLength = ShortLength)]
public sealed class DisplayContainerPacket : BasePacket<DisplayContainerPacket>, IOutgoingPacket
{
    private const int ShortLength = 7;
    private const int HighSeasLength = 9;
    private const ushort ContainerType = 0x7D;

    public override int Length { get; }

    public Serial Container { get; }

    public int Gump { get; }

    public bool HighSeas { get; }

    public DisplayContainerPacket(Serial container, int gump, bool highSeas)
    {
        Container = container;
        Gump = gump;
        HighSeas = highSeas;
        Length = highSeas ? HighSeasLength : ShortLength;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Container);
        writer.WriteUInt16BigEndian((ushort)Gump);

        if (HighSeas)
        {
            writer.WriteUInt16BigEndian(ContainerType);
        }
    }
}
