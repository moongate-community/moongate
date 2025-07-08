using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class DrawGamePlayerPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public DrawGamePlayerPacket() : base(0x20)
    {
    }

    public DrawGamePlayerPacket(UOMobileEntity mobile) : this()
    {
        Mobile = mobile;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        /**
  * BYTE cmd
BYTE[4] creature id
BYTE[2] bodyType
BYTE[1] unknown1 (0)
BYTE[2] skin color / hue
BYTE[1] flag byte
BYTE[2] xLoc
BYTE[2] yLoc
BYTE[2] unknown2 (0)
BYTE[1] direction
BYTE[1] zLoc
  */

        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.Write((short)Mobile.Body);
        writer.Write((byte)0); // unknown1
        writer.Write((ushort)Mobile.SkinHue);
        writer.Write((byte)Mobile.GetPacketFlags(true)); // flag byte

        writer.Write((ushort)Mobile.X);       // 2 bytes (xLoc)
        writer.Write((ushort)Mobile.Y);       // 2 bytes (yLoc)
        writer.Write((ushort)0);              // 2 bytes (unknown2)
        writer.Write((byte)Mobile.Direction); // 1 byte (direction)

        writer.Write((byte)Mobile.Z);




        return writer.ToArray();
    }
}
