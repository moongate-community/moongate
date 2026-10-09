using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Vendors;

/// <summary>
///     Closes the shop window of a vendor (0x3B): the vendor and a count of bought lines, always zero.
/// </summary>
[PacketHandler(0x3B, PacketSizing.Fixed, Length = 8, Description = "Vendor: end of the shop window")]
public sealed class VendorEndPacket : BaseFixedPacket<VendorEndPacket>, IOutgoingPacket
{
    public Serial Vendor { get; }

    public VendorEndPacket(Serial vendor)
    {
        Vendor = vendor;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Vendor);
        writer.WriteByte(0);
    }
}
