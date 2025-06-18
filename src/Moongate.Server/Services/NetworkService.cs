using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using Moongate.Core.Directories;
using Moongate.Core.Network.Extensions;
using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Data.Internal.NetworkService;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Spans;
using Serilog;

namespace Moongate.Server.Services;

public class NetworkService : INetworkService
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();

    public event INetworkService.ClientConnectedHandler? OnClientConnected;
    public event INetworkService.ClientDisconnectedHandler? OnClientDisconnected;
    public event INetworkService.ClientDataReceivedHandler? OnClientDataReceived;
    public event INetworkService.ClientDataSentHandler? OnClientDataSent;
    public event INetworkService.PacketSentHandler? OnPacketSent;
    public event INetworkService.PacketReceivedHandler? OnPacketReceived;

    private readonly Dictionary<byte, INetworkService.PacketHandlerDelegate> _handlers = new();
    private readonly Dictionary<byte, PacketDefinitionData> _packetDefinitions = new();

    private readonly Dictionary<byte, Func<IUoNetworkPacket>> _packetBuilders = new();

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
        foreach (var ipAddress in GetListeningAddresses(new IPEndPoint(IPAddress.Any, _moongateServerConfig.Network.Port)))
        {
            StartServer(new IPEndPoint(ipAddress.Address, _moongateServerConfig.Network.Port));
        }
    }

    private void StartServer(IPEndPoint endPoint)
    {
        try
        {
            var tcpServer = new MoongateTcpServer(endPoint.ToString(), endPoint);

            tcpServer.OnClientConnected += OnTcpClientConnected;
            tcpServer.OnClientDisconnected += OnTcpClientDisconnect;
            tcpServer.OnClientDataReceived += OnDataReceived;

            tcpServer.Start();

            _tcpServers.Add(tcpServer);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start TCP server on {EndPoint}", endPoint);
            throw new InvalidOperationException($"Failed to start TCP server on {endPoint}", ex);
        }
    }

    private void OnDataReceived(MoongateTcpClient client, ReadOnlyMemory<byte> buffer)
    {
        OnClientDataReceived?.Invoke(client.Id, new ReadOnlyMemory<byte>(buffer.ToArray()));
        var remainingBuffer = buffer;

        _logger.Verbose("Received buffer from client {ClientId}: {Length} bytes", client.Id, buffer.Length);

        /// FIXME: Not beautiful, but it works
        if (remainingBuffer.Length == 69)
        {
            remainingBuffer = remainingBuffer[4..];
        }

        while (remainingBuffer.Length > 0)
        {
            var span = remainingBuffer.Span;

            if (span.Length < 1)
            {
                break;
            }

            byte opcode = span[0];


            if (!_packetDefinitions.TryGetValue(opcode, out var packetDefinition))
            {
                _logger.Warning("No size defined for packet opcode: 0x{Opcode:X2}", opcode);
                break;
            }


            _logger.Verbose(
                "Processing packet with opcode: 0x{Opcode:X2}, expected size: {ExpectedSize} bytes",
                opcode,
                packetDefinition.Length == -1 ? "on byte[2]" : packetDefinition.Length
            );

            var headerSize = 1;
            int packetSize;

            if (packetDefinition.Length == -1)
            {
                if (span.Length < 2)
                {
                    break;
                }

                headerSize = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(1, 3));
                packetSize = headerSize;


                if (span.Length < packetSize)
                {
                    break;
                }
            }
            else
            {
                packetSize = packetDefinition.Length;

                if (span.Length < packetSize)
                {
                    break;
                }
            }


            var currentPacketBuffer = remainingBuffer[..packetSize];

            using var packetBuffer = new SpanReader(currentPacketBuffer.Span);

            if (!_packetBuilders.TryGetValue(packetBuffer.ReadByte(), out var packetBuilder))
            {
                _logger.Warning(
                    "No packet builder found for opcode: 0x{Opcode:X2} ({PacketName})",
                    opcode,
                    packetDefinition.Description
                );
                break;
            }

            var packet = packetBuilder();

            try
            {
                var success = packet.Read(currentPacketBuffer.Span.ToArray());


                if (!success)
                {
                    _logger.Warning("Failed to read packet with opcode: 0x{Opcode:X2}", opcode);
                    break;
                }

                DispatchPacket(client.Id, packet);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading packet with opcode: 0x{Opcode:X2}", opcode);
            }


            remainingBuffer = remainingBuffer[packetSize..];
        }


        if (remainingBuffer.Length > 0)
        {
            _logger.Debug("Remaining unprocessed data: {Length} bytes", remainingBuffer.Length);
        }
    }

    private void InternalDispatchPacket(
        string sessionId, IUoNetworkPacket packet, INetworkService.PacketHandlerDelegate handler
    )
    {
        MoongateContext.EnqueueAction(
            $"network_service_session_{sessionId}_opcode{packet.OpCode:X2}_handle_packet",
            () =>
            {
                try
                {
                    handler(sessionId, packet);
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        ex,
                        "Error handling packet with OpCode: 0x{OpCode:X2} for session {SessionId}",
                        packet.OpCode,
                        sessionId
                    );
                    throw new InvalidOperationException(
                        $"Error handling packet with OpCode: 0x{packet.OpCode:X2} for session {sessionId}",
                        ex
                    );
                }
            }
        );
    }

    private void DispatchPacket(string sessionId, IUoNetworkPacket packet)
    {
        var opCode = packet.OpCode;

        _logger.Verbose("Dispatching packet with OpCode: 0x{OpCode:X2} for session {SessionId}", opCode, sessionId);

        if (_handlers.TryGetValue(opCode, out var handler))
        {
            InternalDispatchPacket(sessionId, packet, handler);
        }
        else
        {
            _logger.Warning("No handler registered for packet OpCode: 0x{OpCode:X2}", opCode);
        }

        OnPacketReceived?.Invoke(sessionId, packet);
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
        _logger.Debug(
            "Registered packet: OpCode={OpCode}, Length={Length}, Description={Description}",
            opCode.ToPacketString(),
            length,
            description
        );
    }

    public void BindPacket<TPacket>() where TPacket : IUoNetworkPacket, new()
    {
        var packet = new TPacket();
        _packetBuilders[packet.OpCode] = () => new TPacket();
    }

    public bool IsPacketBound<TPacket>() where TPacket : IUoNetworkPacket, new()
    {
        var packet = new TPacket();
        return _packetBuilders.ContainsKey(packet.OpCode);
    }

    public void RegisterPacketHandler<TPacket>(INetworkService.PacketHandlerDelegate handler)
        where TPacket : IUoNetworkPacket, new()
    {
        byte opCode = new TPacket().OpCode;

        RegisterPacketHandler(opCode, handler);
    }

    public void RegisterPacketHandler(byte opCode, INetworkService.PacketHandlerDelegate handler)
    {
        if (!_handlers.TryGetValue(opCode, out var existHandler))
        {
            _handlers[opCode] = handler;
        }
        else
        {
            _handlers[opCode] = existHandler + handler;
        }

        _logger.Information("Registered handler for packet OpCode {OpCode}", opCode.ToPacketString());
    }

    private int GetPacketSize(IUoNetworkPacket packet)
    {
        if (_packetDefinitions.TryGetValue(packet.OpCode, out var definition))
        {
            return definition.Length;
        }

        _logger.Warning("No size defined for packet OpCode: {OpCode}", packet.OpCode.ToPacketString());
        return -1; // Indicating unknown size
    }

    public void SendPacket(MoongateTcpClient client, IUoNetworkPacket packet)
    {
        var size = GetPacketSize(packet);
        var spanWriter = new SpanWriter(size == -1 ? 1 : size, size == -1);
        var packetData = packet.Write(spanWriter);

        OnPacketSent?.Invoke(client.Id, packet);
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

            OnClientDataSent?.Invoke(client.Id, data);
        }
        catch (Exception ex)
        {
            _logger.Error("Error sending packet to client {ClientId}: {ErrorMessage}", client.Id, ex.Message);
        }
    }

    public void BroadcastPacket(IUoNetworkPacket packet)
    {
        var size = GetPacketSize(packet);
        var spanWriter = new SpanWriter(size, size != -1);
        var packetData = packet.Write(spanWriter);
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


    public static IEnumerable<IPEndPoint> GetListeningAddresses(IPEndPoint ipep) =>
        NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(adapter =>
                adapter.GetIPProperties()
                    .UnicastAddresses
                    .Where(uip => ipep.AddressFamily == uip.Address.AddressFamily)
                    .Select(uip => new IPEndPoint(uip.Address, ipep.Port))
            );

    public void Dispose()
    {
        _clientsLock.Dispose();
    }
}
