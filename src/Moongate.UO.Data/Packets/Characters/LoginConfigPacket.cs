using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class LoginConfigPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public LoginConfigPacket(UOMobileEntity mobile) : this()
    {
        Mobile = mobile;
    }

    public LoginConfigPacket() : base(0x1B)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.Write(0);
        writer.Write((short)Mobile.Body);
        writer.Write((short)Mobile.Location.X);
        writer.Write((short)Mobile.Location.Y);
        writer.Write((byte)0);
        writer.Write((byte)Mobile.Location.Z);
        writer.Write((byte)Mobile.Direction);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)0);
        writer.Write((short)Mobile.Map.Width);
        writer.Write((short)Mobile.Map.Height);
        writer.Write((short)0);
        writer.Write(0);

        return writer.ToArray();
    }
}
