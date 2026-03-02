using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Incoming.Movement;

[PacketHandler(0x72, PacketSizing.Fixed, Length = 5, Description = "Request War Mode")]

/// <summary>
/// Represents RequestWarModePacket.
/// </summary>
public class RequestWarModePacket : BaseGameNetworkPacket
{
    public bool IsWarMode { get; private set; }

    public RequestWarModePacket()
        : base(0x72, 5) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 4)
        {
            return false;
        }

        IsWarMode = reader.ReadByte() != 0;
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();

        return true;
    }
}
