using System.Net;
using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Data.Internal.NetworkService;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Serilog;

namespace Moongate.Core.Server.Services;

public class NetworkService : INetworkService
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();

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


    private readonly MoongateServerConfig _moongateServerConfig;

    public NetworkService(MoongateServerConfig moongateServerConfig)
    {
        _moongateServerConfig = moongateServerConfig;
    }


    public async Task StartAsync(CancellationToken cancellationToken = default)

    {
    }

    private void StartServer(IPEndPoint endPoint)
    {
        try
        {
            var tcpServer = new MoongateTcpServer(endPoint.ToString(), endPoint);

            tcpServer.OnClientConnected += OnTcpClientConnected;
            tcpServer.OnClientDisconnected += OnTcpClientDisconnect;
            tcpServer.OnClientDataReceived += OnDataReceived;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start TCP server on {EndPoint}", endPoint);
            throw new InvalidOperationException($"Failed to start TCP server on {endPoint}", ex);
        }
    }

    private void OnDataReceived(MoongateTcpClient client, ReadOnlyMemory<byte> buffer)
    {
    }

    private void OnTcpClientDisconnect(MoongateTcpClient obj)
    {
        _clientsLock.Wait();
        try
        {
            _clients.Remove(obj);
            OnClientDisconnected?.Invoke(obj.Id, obj);
            _logger.Information("Client disconnected: {ClientId}", obj.Id);
        }
        finally
        {
            _clientsLock.Release();
        }
    }

    private void OnTcpClientConnected(MoongateTcpClient obj)
    {
        _clientsLock.Wait();
        try
        {
            _clients.Add(obj);
            OnClientConnected?.Invoke(obj.Id, obj);
            _logger.Information("Client connected: {ClientId}", obj.Id);
        }
        finally
        {
            _clientsLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Stopping NetworkService...");

        _clientsLock.Wait(cancellationToken);
        try
        {
            foreach (var client in _clients)
            {
                client.Disconnect();
            }
            _clients.Clear();
        }
        finally
        {
            _clientsLock.Release();
        }

        foreach (var server in _tcpServers)
        {
            server.Stop();
        }

        _tcpServers.Clear();

        _logger.Information("NetworkService stopped.");
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

        SendPacket(client, packetData);
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
