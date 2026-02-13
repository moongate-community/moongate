using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Packets.Characters;

public class UpdatePlayerPacket : BaseUoPacket
{
    public UOMobileEntity Mobile { get; set; }

    public UpdatePlayerPacket() : base(0x77) { }

    public UpdatePlayerPacket(UOMobileEntity mobile) : this()
        => Mobile = mobile;

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.Write((short)Mobile.Body);
        writer.Write((short)Mobile.Location.X);
        writer.Write((short)Mobile.Location.Y);
        writer.Write((sbyte)Mobile.Location.Z);
        writer.Write((byte)Mobile.Direction);
        writer.Write((short)Mobile.SkinHue);
        writer.Write(Mobile.GetPacketFlags(true));
        writer.Write((byte)Mobile.Notoriety);

        /**
         *
         *  Packet Name: Update Player
            Last Modified: 2008-10-11 12:57:22
            Modified By: MuadDib

            Packet: 0x77
            Sent By: Server
            Size: 17 Bytes

            Packet Build
            BYTE[1] cmd
            BYTE[4] player id
            BYTE[2] model
            BYTE[2] xLoc
            BYTE[2] yLoc
            BYTE[1] zLoc
            BYTE[1] direction
            BYTE[2] hue/skin color
            BYTE[1] status flag (bit field)
            BYTE[1] highlight color

Subcommand Build
N/A

Notes
01-10-2007
Interesting, 0x12 Status Flag was a guild mates mount not in war mode.
0x52 is for the same pets status flag while in war mode. Poisoned it stayed the same appropriately in each. 0x12 and 0x52
         */

        return writer.ToArray();
    }
}
