using Moongate.Network.Interfaces;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Support features (0xB9): unlocks the client feature set at login, sent right before the
/// character list. Moongate targets modern (7.x) clients only, so it always writes the extended
/// 4-byte flags form.
/// </summary>
public readonly record struct SupportFeaturesPacket(FeatureFlagType Flags) : IOutgoingPacket
{
    public const byte PacketId = 0xB9;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((uint)Flags);
    }
}
