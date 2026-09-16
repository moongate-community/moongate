using Moongate.Core.Types.Expansions;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Outgoing.Login;

/// <summary>
/// Enables client capabilities using the 32-bit feature mask supported by ClassicUO 7.x.
/// </summary>
[PacketHandler(0xB9, PacketSizing.Fixed, Length = 5, Description = "Enable Locked Client Features")]
public sealed class SupportFeaturesPacket : BaseFixedPacket<SupportFeaturesPacket>, IOutgoingPacket
{
    public FeatureFlags Flags { get; }

    public SupportFeaturesPacket(FeatureFlags flags)
    {
        Flags = flags;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt32BigEndian((uint)Flags);
    }
}
