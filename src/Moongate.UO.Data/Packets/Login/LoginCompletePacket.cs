using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Login;

public class LoginCompletePacket : BaseUoPacket
{
    public LoginCompletePacket() : base(0x55)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        return writer.ToArray();
    }
}
