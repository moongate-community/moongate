using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Player;

[PacketHandler(0xC8, PacketSizing.Fixed, Length = 2, Description = "Client View Range")]

/// <summary>
/// Represents ClientViewRangePacket.
/// </summary>
public class ClientViewRangePacket : BaseGameNetworkPacket
{
    public const byte MinRange = 5;
    public const byte MaxRange = 18;

    public byte Range { get; set; } = MaxRange;

    public ClientViewRangePacket()
        : base(0xC8, 2) { }

    public ClientViewRangePacket(byte range)
        : this()
    {
        Range = range;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Range);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 1)
        {
            return false;
        }

        Range = reader.ReadByte();

        return reader.Remaining == 0;
    }
}
