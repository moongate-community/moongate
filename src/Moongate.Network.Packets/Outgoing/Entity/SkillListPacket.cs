using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0x3A, PacketSizing.Variable, Description = "Send Skills")]
public class SkillListPacket : BaseGameNetworkPacket
{
    public UOMobileEntity? Mobile { get; set; }

    public SendSkillResponseType ResponseType { get; set; } = SendSkillResponseType.FullSkillList;

    public SkillListPacket()
        : base(0x3A) { }

    public SkillListPacket(UOMobileEntity mobile)
        : this()
    {
        Mobile = mobile;
    }

    public override void Write(ref SpanWriter writer)
    {
        if (Mobile is null)
        {
            throw new InvalidOperationException("Mobile must be set before writing SkillListPacket.");
        }

        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write((byte)ResponseType);

        foreach (var skill in Mobile.Skills.OrderBy(static pair => (int)pair.Key))
        {
            writer.Write((ushort)((int)skill.Key + 1));
            writer.Write((ushort)Math.Clamp((int)skill.Value.Value, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp((int)skill.Value.Base, 0, ushort.MaxValue));
            writer.Write((byte)skill.Value.Lock);
        }

        writer.Write((ushort)0);
        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => true;
}
