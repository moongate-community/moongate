using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Base;

public abstract class BaseFixedPacket<TPacket> : BasePacket<TPacket>
    where TPacket : class, IPacket
{
    public sealed override int Length { get; }

    protected BaseFixedPacket()
    {
        if (Descriptor.Sizing != PacketSizing.Fixed || Descriptor.FixedLength is not int fixedLength)
        {
            throw new InvalidOperationException(
                $"Packet type '{typeof(TPacket).FullName}' uses BaseFixedPacket but is not fixed-size."
            );
        }

        Length = fixedLength;
    }
}
