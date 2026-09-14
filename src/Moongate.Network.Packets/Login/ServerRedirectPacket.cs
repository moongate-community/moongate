using System.Net;

using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Login;

[PacketHandler(0x8C, PacketSizing.Fixed, Length = 11)]
public sealed class ServerRedirectPacket : BaseFixedPacket<ServerRedirectPacket>, IOutgoingPacket
{
    private readonly byte[] _addressBytes;

    public IPAddress Address => new(_addressBytes);
    public ushort Port { get; }
    public uint AuthKey { get; }

    public ServerRedirectPacket(IPAddress address, ushort port, uint authKey)
    {
        _addressBytes = PacketValidation.SnapshotIPv4(address, nameof(address));
        Port = port;
        AuthKey = authKey;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteBytes(_addressBytes);
        writer.WriteUInt16BigEndian(Port);
        writer.WriteUInt32BigEndian(AuthKey);
    }
}
