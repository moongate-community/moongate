using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Login;

[PacketHandler(0xB9, PacketSizing.Variable, Description = "Enable locked client features")]

/// <summary>
/// Represents SupportFeaturesPacket.
/// </summary>
public class SupportFeaturesPacket : BaseGameNetworkPacket
{
    private const int ExtendedLength = 5;
    private const int LegacyLength = 3;

    public FeatureFlags Flags { get; set; }

    public bool UseExtendedFormat { get; set; }

    public SupportFeaturesPacket()
        : this(GetDefaultFlags(), true) { }

    public SupportFeaturesPacket(FeatureFlags flags, bool useExtendedFormat)
        : base(0xB9, useExtendedFormat ? ExtendedLength : LegacyLength)
    {
        Flags = flags;
        UseExtendedFormat = useExtendedFormat;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);

        if (UseExtendedFormat)
        {
            writer.Write((uint)Flags);
        }
        else
        {
            writer.Write((ushort)Flags);
        }
    }

    protected override bool ParsePayload(ref SpanReader reader)
        => true;

    private static FeatureFlags GetDefaultFlags()
        => ExpansionInfo.Table is { Length: > 0 } ? ExpansionInfo.CoreExpansion.SupportedFeatures : FeatureFlags.ExpansionEJ;
}
