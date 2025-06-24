using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface INetworkService : IMoongateAutostartService
{
    delegate void ClientConnectedHandler(string clientId, MoongateTcpClient client );
    delegate void ClientDisconnectedHandler(string clientId, MoongateTcpClient client );
    delegate void ClientDataReceivedHandler(string clientId, ReadOnlyMemory<byte>data);
    delegate void ClientDataSentHandler(string clientId, ReadOnlyMemory<byte> data);
    delegate Task PacketHandlerDelegate(string sessionId, IUoNetworkPacket packet);
    delegate Task PacketSentHandler(string sessionId, IUoNetworkPacket packet);
    delegate Task PacketReceivedHandler(string sessionId, IUoNetworkPacket packet);

    event ClientConnectedHandler OnClientConnected;
    event ClientDisconnectedHandler OnClientDisconnected;
    event ClientDataReceivedHandler OnClientDataReceived;

    event ClientDataSentHandler OnClientDataSent;
    event PacketSentHandler OnPacketSent;
    event PacketReceivedHandler OnPacketReceived;

    void RegisterPacket(byte opCode, int length, string description);

    string GetPacketDescription(byte opCode);

    void BindPacket<TPacket>()
        where TPacket : IUoNetworkPacket, new();

    bool IsPacketBound<TPacket>()
        where TPacket : IUoNetworkPacket, new();

    void RegisterPacketHandler<TPacket>(PacketHandlerDelegate handler)
        where TPacket : IUoNetworkPacket, new();

    void RegisterPacketHandler(byte opCode, PacketHandlerDelegate handler);

    void SendPacket(MoongateTcpClient client, IUoNetworkPacket packet);
    void SendPacket(MoongateTcpClient client, ReadOnlyMemory<byte> data);

    void BroadcastPacket(IUoNetworkPacket packet);
    void BroadcastPacket(ReadOnlyMemory<byte> data);


}
