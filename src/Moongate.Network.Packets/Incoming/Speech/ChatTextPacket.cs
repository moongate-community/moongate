using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Types.Speech;

namespace Moongate.Network.Packets.Incoming.Speech;

[PacketHandler(0xB3, PacketSizing.Variable, Description = "Chat Text")]

/// <summary>
/// Represents ChatTextPacket.
/// </summary>
public class ChatTextPacket : BaseGameNetworkPacket
{
    public string Language { get; private set; } = "ENU";

    public ChatActionType ActionId { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public ChatTextPacket()
        : base(0xB3) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 2)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || reader.Remaining < 6)
        {
            return false;
        }

        Language = reader.ReadAsciiSafe(4);
        ActionId = (ChatActionType)reader.ReadInt16();
        Payload = reader.ReadBigUniSafe().TrimEnd();

        return true;
    }
}
