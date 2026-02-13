using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class LoginConfigPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public LoginConfigPacket(UOMobileEntity mobile) : this()
        => Mobile = mobile;

    public LoginConfigPacket() : base(0x1B) { }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);                        // 0: opcode 0x1B
        writer.Write(Mobile.Id.Value);               // 1-4: serial
        writer.Write(0);                             // 5-8: unknown (always 0)
        writer.Write((short)Mobile.Body);            // 9-10: body
        writer.Write((short)Mobile.Location.X);      // 11-12: x
        writer.Write((short)Mobile.Location.Y);      // 13-14: y
        writer.Write((short)Mobile.Location.Z);      // 15-16: z (signed short)
        writer.Write((byte)Mobile.Direction);        // 17: direction
        writer.Write((byte)0);                       // 18: padding
        writer.Write(-1);                            // 19-22: unknown (always 0xFFFFFFFF)
        writer.Write(0);                             // 23-26: unknown (always 0)
        writer.Write((short)Mobile.Map.Width);       // 27-28: map width
        writer.Write((short)Mobile.Map.Height);      // 29-30: map height
        writer.Clear(37 - writer.Position);          // 31-36: zero fill remaining

        return writer.ToArray();
    }
}
