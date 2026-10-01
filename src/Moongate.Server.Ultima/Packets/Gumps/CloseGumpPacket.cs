using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Gumps;

/// <summary>
///     Closes a gump on the client: general information packet 0xBF, subcommand 0x04, with the gump's type id and the
///     button the client answers with.
/// </summary>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = 5)]
public sealed class CloseGumpPacket : BasePacket<CloseGumpPacket>, IOutgoingPacket
{
    private const ushort Subcommand = 0x04;

    public override int Length => 13;

    public uint TypeId { get; }

    public int ButtonId { get; }

    public CloseGumpPacket(uint typeId, int buttonId)
    {
        TypeId = typeId;
        ButtonId = buttonId;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian(Subcommand);
        writer.WriteUInt32BigEndian(TypeId);
        writer.WriteUInt32BigEndian((uint)ButtonId);
    }
}
