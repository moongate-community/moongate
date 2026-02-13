using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Events.Features;

public class SupportFeaturesPacket : BaseUoPacket
{
    public FeatureFlags Flags { get; set; }

    public SupportFeaturesPacket() : base(0xB9)
    {
        Flags = ExpansionInfo.CoreExpansion.SupportedFeatures;
        Flags |= FeatureFlags.LiveAccount;
        Flags |= FeatureFlags.SeventhCharacterSlot;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        //16749275

        //185
        //0
        //255
        //146
        //219
        writer.Write((uint)Flags);

        return writer.ToArray();
    }
}
