namespace Moongate.Network.Packets.Types.Packets;

[Flags]
public enum PacketDirection
{
    None = 0,
    Incoming = 1,
    Outgoing = 2,
    Both = Incoming | Outgoing
}
