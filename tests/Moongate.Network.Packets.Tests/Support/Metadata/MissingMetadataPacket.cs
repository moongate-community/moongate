using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Support.Metadata;

public sealed class MissingMetadataPacket : BaseFixedPacket<MissingMetadataPacket>, IOutgoingPacket
{
    public void Write(ref PacketWriter writer) { }
}
