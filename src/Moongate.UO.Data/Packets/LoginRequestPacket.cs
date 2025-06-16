using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets;

public class LoginRequestPacket : BaseUoPacket
{

    public string Account { get; set; }

    public string Password { get; set; }

    public byte NextLoginKey { get; set; }

    public LoginRequestPacket() : base(0x80)
    {
    }

    protected override bool Read(SpanReader reader)
    {
        Account = reader.ReadAscii(30);
        Password = reader.ReadAscii(30);
        NextLoginKey = reader.ReadByte();

        return true;
    }
}
