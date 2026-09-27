using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Login;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Outgoing.Login;

/// <summary>
///     Shows one of the client's built-in login popups (0x53); a refusal is followed by a disconnect.
/// </summary>
[PacketHandler(0x53, PacketSizing.Fixed, Length = 2)]
public sealed class PopupMessagePacket : BaseFixedPacket<PopupMessagePacket>, IOutgoingPacket
{
    public PopupMessageType Type { get; }

    public PopupMessagePacket(PopupMessageType type)
    {
        Type = type;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Type);
    }
}
