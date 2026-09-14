using System.Net;

using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class ServerRedirectPacket : IOutgoingPacket
{
    private const byte PacketOpCode = 0x8C;
    private const int PacketLength = 11;
    private readonly byte[] _addressBytes;

    public byte OpCode => PacketOpCode;
    public int Length => PacketLength;
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
