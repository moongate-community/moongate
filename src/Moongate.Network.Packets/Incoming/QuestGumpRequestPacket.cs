using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming;

[PacketHandler(0xD7, PacketSizing.Variable, Description = "Quest Gump Request")]

/// <summary>
/// Represents QuestGumpRequestPacket.
/// </summary>
public sealed class QuestGumpRequestPacket : BaseGameNetworkPacket
{
    private const ushort QuestButtonCommandId = 0x0032;
    private const byte TerminatorValue = 0x07;
    private const int MinimumPacketLength = 10;

    /// <summary>
    /// Gets the character serial encoded in the packet payload.
    /// </summary>
    public Serial PlayerSerial { get; private set; }

    /// <summary>
    /// Gets the encoded command identifier.
    /// </summary>
    public ushort EncodedCommandId { get; private set; }

    /// <summary>
    /// Gets the trailing terminator byte.
    /// </summary>
    public byte Terminator { get; private set; }

    public QuestGumpRequestPacket()
        : base(0xD7) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < MinimumPacketLength - 1)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != MinimumPacketLength || declaredLength != reader.Length)
        {
            return false;
        }

        PlayerSerial = (Serial)reader.ReadUInt32();
        EncodedCommandId = reader.ReadUInt16();

        if (EncodedCommandId != QuestButtonCommandId || reader.Remaining != 1)
        {
            return false;
        }

        Terminator = reader.ReadByte();

        return Terminator == TerminatorValue && reader.Remaining == 0;
    }
}
