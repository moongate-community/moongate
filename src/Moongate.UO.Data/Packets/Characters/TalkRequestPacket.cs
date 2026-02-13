using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Packets.Characters;

/// <summary>
/// OpCode 0x03 - Talk Request
/// Client->Server: Player initiates NPC conversation
/// </summary>
public class TalkRequestPacket : BaseUoPacket
{
    public TalkRequestPacket() : base(0x03) { }

    public Serial NpcSerial { get; set; }

    protected override bool Read(SpanReader reader)
    {
        NpcSerial = (Serial)reader.ReadUInt32();
        return NpcSerial.IsValid;
    }
}
