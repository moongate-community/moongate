using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x77, PacketSizing.Fixed, Length = 17, Description = "Mobile Moving")]
public sealed class MobileMovingPacket : BaseGameNetworkPacket
{
    public UOMobileEntity? Mobile { get; set; }

    public bool StygianAbyss { get; set; } = true;

    public MobileMovingPacket()
        : base(0x77, 17) { }

    public MobileMovingPacket(UOMobileEntity mobile, bool stygianAbyss = true)
        : this()
    {
        Mobile = mobile;
        StygianAbyss = stygianAbyss;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Mobile is null)
        {
            throw new InvalidOperationException("Mobile must be set before writing MobileMovingPacket.");
        }

        writer.Write(OpCode);
        writer.Write(Mobile.Id.Value);
        writer.Write((short)Mobile.Body);
        writer.Write((short)Mobile.Location.X);
        writer.Write((short)Mobile.Location.Y);
        writer.Write((sbyte)Mobile.Location.Z);
        writer.Write((byte)Mobile.Direction);
        writer.Write((short)Mobile.SkinHue);
        writer.Write(Mobile.GetPacketFlags(StygianAbyss));
        writer.Write((byte)Mobile.Notoriety);
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => reader.Remaining == 16;
}
