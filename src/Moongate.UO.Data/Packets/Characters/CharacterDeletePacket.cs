using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Characters;

public class CharacterDeletePacket : BaseUoPacket
{
    public int Index { get; set; }

    public CharacterDeletePacket() : base(0x83) { }

    protected override bool Read(SpanReader reader)
    {
        reader.ReadBytes(30);
        Index = reader.ReadInt32();
        reader.ReadInt32();

        return true;
    }
}
