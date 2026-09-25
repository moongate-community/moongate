using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;

namespace Moongate.Network.Packets.Base;

public abstract class BasePacket<TPacket> : IPacket
    where TPacket : class, IPacket
{
    public static PacketDescriptor Descriptor => PacketMetadataCache<TPacket>.Descriptor;
    public byte OpCode => Descriptor.OpCode;
    public abstract int Length { get; }

    protected static bool HasValidHeader(ReadOnlySpan<byte> data)
    {
        return PacketValidation.HasValidHeader(data, Descriptor);
    }
}
