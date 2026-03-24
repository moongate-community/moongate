using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.Network.Packets.Incoming.Player;

[PacketHandler(0xB8, PacketSizing.Variable, Description = "Request/Char Profile")]

/// <summary>
/// Represents RequestCharProfilePacket.
/// </summary>
public class RequestCharProfilePacket : BaseGameNetworkPacket
{
    private const int MaximumProfileCharacters = 511;

    public byte Mode { get; private set; }

    public Serial TargetId { get; private set; }

    public ushort? CommandType { get; private set; }

    public ushort? CharacterCount { get; private set; }

    public string? ProfileText { get; private set; }

    public RequestCharProfilePacket()
        : base(0xB8) { }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 7)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length || reader.Remaining < 5)
        {
            return false;
        }

        Mode = reader.ReadByte();
        TargetId = (Serial)reader.ReadUInt32();
        CommandType = null;
        CharacterCount = null;
        ProfileText = null;

        if (Mode == 0x00)
        {
            return reader.Remaining == 0;
        }

        if (Mode != 0x01 || reader.Remaining < 4)
        {
            return false;
        }

        CommandType = reader.ReadUInt16();
        CharacterCount = reader.ReadUInt16();

        if (CharacterCount.Value > MaximumProfileCharacters)
        {
            return false;
        }

        var requiredBytes = CharacterCount.Value * 2;

        if (reader.Remaining != requiredBytes)
        {
            return false;
        }

        ProfileText = CharacterCount.Value == 0 ? string.Empty : reader.ReadBigUni(CharacterCount.Value);

        return reader.Remaining == 0;
    }
}
