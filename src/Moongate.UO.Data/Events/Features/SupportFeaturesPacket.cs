using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Events.Features;

public class SupportFeaturesPacket : BaseUoPacket
{
    public FeatureFlags Flags { get; set; }

    public SupportFeaturesPacket() : base(0xB9)
    {
        Flags |= FeatureFlags.LiveAccount;
        Flags &= ~FeatureFlags.UOTD;

        Flags |= FeatureFlags.SixthCharacterSlot;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((int)Flags);
        return writer.ToArray();
    }
}
