using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface INetworkService : IMoongateAutostartService
{
    delegate void ClientConnectedHandler(string clientId, MoongateTcpClient client );
    delegate void ClientDisconnectedHandler(string clientId, MoongateTcpClient client );
    delegate void ClientDataReceivedHandler(string clientId, ReadOnlyMemory<byte>data);
    delegate Task PacketHandlerDelegate(string sessionId, IUoNetworkPacket packet);

    event ClientConnectedHandler OnClientConnected;
    event ClientDisconnectedHandler OnClientDisconnected;
    event ClientDataReceivedHandler OnClientDataReceived;

    void RegisterPacket(byte opCode, int length, string description);

    void RegisterPacketHandler<TPacket>(PacketHandlerDelegate handler)
        where TPacket : IUoNetworkPacket, new();

    void RegisterPacketHandler(byte opCode, PacketHandlerDelegate handler);

    void SendPacket(string clientId, IUoNetworkPacket packet);
    void SendPacket(string clientId, ReadOnlyMemory<byte> data);

    void BroadcastPacket(IUoNetworkPacket packet);
    void BroadcastPacket(ReadOnlyMemory<byte> data);


}
