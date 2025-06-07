using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Data.Internal.NetworkService;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Serilog;

namespace Moongate.Core.Server.Services;

public class NetworkService : INetworkService
{
    public event INetworkService.ClientConnectedHandler? OnClientConnected;
    public event INetworkService.ClientDisconnectedHandler? OnClientDisconnected;
    public event INetworkService.ClientDataReceivedHandler? OnClientDataReceived;
    public event INetworkService.PacketSentHandler? OnPacketSent;
    public event INetworkService.PacketReceivedHandler? OnPacketReceived;

    private readonly Dictionary<byte, INetworkService.PacketHandlerDelegate> _handlers = new();
    private readonly Dictionary<byte, PacketDefinitionData> _packetDefinitions = new();

    private readonly List<MoongateTcpServer> _tcpServers = new();

    private readonly SemaphoreSlim _clientsLock = new(1, 1);
    private readonly List<MoongateTcpClient> _clients = new();

    private readonly ILogger _logger = Log.ForContext<NetworkService>();

    public NetworkService()
    {

    }


    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void RegisterPacket(byte opCode, int length, string description)
    {
        if (_packetDefinitions.ContainsKey(opCode))
        {
            _logger.Warning("Packet with OpCode {OpCode} is already registered.", opCode);
            return;
        }

        _packetDefinitions[opCode] = new PacketDefinitionData(opCode, length, description);
        _logger.Information(
            "Registered packet: OpCode={OpCode}, Length={Length}, Description={Description}",
            opCode,
            length,
            description
        );
    }

    public void RegisterPacketHandler<TPacket>(INetworkService.PacketHandlerDelegate handler)
        where TPacket : IUoNetworkPacket, new()
    {
        byte opCode = new TPacket().OpCode;

        RegisterPacketHandler(opCode, handler);
    }

    public void RegisterPacketHandler(byte opCode, INetworkService.PacketHandlerDelegate handler)
    {
        _handlers[opCode] += handler;

        _logger.Information("Registered handler for packet OpCode {OpCode}", "0x" + opCode.ToString("X2"));
    }

    public void SendPacket(MoongateTcpClient client, IUoNetworkPacket packet)
    {
        var packetData = packet.Write();
    }

    public void SendPacket(MoongateTcpClient client, ReadOnlyMemory<byte> data)
    {
        var opCode = data.Span[0];
        MoongateContext.EnqueueAction(
            $"network_service_session_{client.Id}_opcode{opCode:X2}_send_packet",
            () => InternalSendPacket(client, data)
        );
    }

    private void InternalSendPacket(MoongateTcpClient client, ReadOnlyMemory<byte> data)
    {
        try
        {
            client.Send(data);
            OnPacketSent?.Invoke(client.Id, data);
        }
        catch (Exception ex)
        {
            _logger.Error("Error sending packet to client {ClientId}: {ErrorMessage}", client.Id, ex.Message);
        }
    }

    public void BroadcastPacket(IUoNetworkPacket packet)
    {
        var packetData = packet.Write();
        MoongateContext.EnqueueAction(
            "network_service_broadcast_packet",
            () => InternalBroadcastPacket(packetData)
        );
    }

    public void BroadcastPacket(ReadOnlyMemory<byte> data)
    {
        MoongateContext.EnqueueAction(
            "network_service_broadcast_packet",
            () => InternalBroadcastPacket(data)
        );
    }

    private void InternalBroadcastPacket(ReadOnlyMemory<byte> data)
    {
        _clientsLock.Wait();
        try
        {
            foreach (var client in _clients)
            {
                try
                {
                    SendPacket(client, data);
                }
                catch (Exception ex)
                {
                    _logger.Error("Error broadcasting packet to client {ClientId}: {ErrorMessage}", client.Id, ex.Message);
                }
            }
        }
        finally
        {
            _clientsLock.Release();
        }
    }
}
