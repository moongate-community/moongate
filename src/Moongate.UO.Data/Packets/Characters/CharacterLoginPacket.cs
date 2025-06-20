using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Characters;

public class CharacterLoginPacket : BaseUoPacket
{

    public string CharacterName { get; set; }
    public ClientFlags ClientFlags { get; set; } = ClientFlags.None;
    public int Index { get; set; }

    public CharacterLoginPacket() : base(0x5D)
    {
    }


    protected override bool Read(SpanReader reader)
    {
        reader.ReadInt32();
        CharacterName = reader.ReadAscii(30);
        reader.ReadInt16();
        ClientFlags = (ClientFlags)reader.ReadUInt32();
        reader.ReadBytes(8);
        reader.ReadBytes(16);
        Index = reader.ReadInt32();
        reader.ReadBytes(4); // Ip, but who cares?
        return true;
    }
}
