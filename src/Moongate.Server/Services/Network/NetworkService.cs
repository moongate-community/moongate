using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Data.Events;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Server;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Serilog;

namespace Moongate.Server.Services.Network;

public class NetworkService : INetworkService
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();

    private readonly MoongateServerConfig _config;

    private readonly ISessionService _sessionService;

    private readonly List<MoongateTcpServer> _tcpServers = [];

    public NetworkService(MoongateServerConfig config, ISessionService sessionService)
    {
        _config = config;
        _sessionService = sessionService;

        PrepareTcpServers();
    }

    private void PrepareTcpServers()
    {
        var ipAddressToBind = new List<IPAddress>();

        if (_config.Network.ListenAddress == "0.0.0.0")
        {
            ipAddressToBind = new List<IPAddress>(NetworkUtils.GetLocalIpAddresses());
        }
        else
        {
            ipAddressToBind.Add(IPAddress.Parse(_config.Network.ListenAddress));
        }

        foreach (var ipAddress in ipAddressToBind)
        {
            var tcpServer = new MoongateTcpServer(new IPEndPoint(ipAddress, _config.Network.GamePort));

            _tcpServers.Add(tcpServer);

            tcpServer.OnClientConnect += TcpServerOnOnClientConnect;

            tcpServer.OnClientDisconnect += TcpServerOnOnClientDisconnect;

            tcpServer.OnDataReceived += TcpServerOnOnDataReceived;
        }
    }

    private void TcpServerOnOnDataReceived(object? sender, TcpDataReceivedEventArgs e)
    {
        _logger.Debug("Received {Length} bytes from client {SessionId}", e.Data.Length, e.Client.SessionId);

        var success = PacketRegistry.Default.TryDecode(e.Data.Span, out var packet, out var opCode);

        if (success)
        {
            _logger.Debug("Decoded packet {PacketType} from client {SessionId}", packet.GetType().Name, e.Client.SessionId);
        }
        else
        {
            var packetName = PacketRegistry.Default.TryGetDescriptor(opCode, out var descriptor)
                                 ? descriptor.PacketType.Name
                                 : "Unknown";

            _logger.Warning(
                "Unknown packet received from client {SessionId}, opCode: {OpCode} name: {PacketName}",
                e.Client.SessionId,
                opCode,
                packetName
            );
        }
    }

    private void TcpServerOnOnClientDisconnect(object? sender, TcpClientEventArgs e)
    {
        if (_sessionService.TryGet(e.Client.SessionId, out var session))
        {
            session.NetworkSession.DetachClient();
            _sessionService.Remove(e.Client.SessionId);
        }
    }

    private void TcpServerOnOnClientConnect(object? sender, TcpClientEventArgs e)
    {
        _logger.Information(
            "Client connected from {Address} with session ID {SessionId}",
            e.Client.RemoteEndPoint,
            e.Client.SessionId
        );
        _sessionService.GetOrCreate(e.Client);
    }

    public Task StartAsync()
    {
        foreach (var tcpServer in _tcpServers)
        {
            _logger.Information(
                "Starting TCP server on {Address}:{Port}",
                tcpServer.Endpoint.Address,
                tcpServer.Endpoint.Port
            );
            tcpServer.StartAsync(default);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        foreach (var tcpServer in _tcpServers)
        {
            _logger.Information(
                "Stopping TCP server on {Address}:{Port}",
                tcpServer.Endpoint.Address,
                tcpServer.Endpoint.Port
            );
            await tcpServer.StopAsync(default);
        }
    }
}
