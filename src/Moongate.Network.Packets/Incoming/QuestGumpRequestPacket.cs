using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming;

[PacketHandler(0xD7, PacketSizing.Variable, Description = "Generic AOS Commands")]

/// <summary>
/// Represents QuestGumpRequestPacket.
/// </summary>
public sealed class QuestGumpRequestPacket : BaseGameNetworkPacket
{
    private const int MinimumPacketLength = 9;

    /// <summary>
    /// Gets the character serial encoded in the packet payload.
    /// </summary>
    public Serial PlayerSerial { get; private set; }

    /// <summary>
    /// Gets the encoded command identifier.
    /// </summary>
    public ushort EncodedCommandId { get; private set; }

    /// <summary>
    /// Gets the encoded payload bytes that follow the encoded command identifier.
    /// </summary>
    public ReadOnlyMemory<byte> EncodedCommandData { get; private set; } = ReadOnlyMemory<byte>.Empty;

    public QuestGumpRequestPacket()
        : base(0xD7) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < MinimumPacketLength - 1)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || declaredLength < MinimumPacketLength)
        {
            return false;
        }

        PlayerSerial = (Serial)reader.ReadUInt32();
        EncodedCommandId = reader.ReadUInt16();
        EncodedCommandData = reader.Remaining == 0 ? ReadOnlyMemory<byte>.Empty : reader.ReadBytes(reader.Remaining);

        return true;
    }
}
