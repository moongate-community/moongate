using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Login;

public class LoginDeniedPacket : BaseUoPacket
{
    public byte Reason { get; set; }

    public LoginDeniedPacket(UOLoginDeniedReason reason) : base(0x82)
        => Reason = (byte)reason;

    public LoginDeniedPacket() : base(0x82) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Reason);

        return writer.ToArray();
    }
}
