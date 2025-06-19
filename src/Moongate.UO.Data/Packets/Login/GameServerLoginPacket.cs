using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Login;

public class GameServerLoginPacket : BaseUoPacket
{
    public uint AuthKey { get; set; }
    public string AccountName { get; set; }
    public string Password { get; set; }

    public GameServerLoginPacket() : base(0x91)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        AuthKey = reader.ReadUInt32();
        AccountName = reader.ReadAscii(30);
        Password = reader.ReadAscii(30);
        return true;
    }
}
