using System.Net;
using Moongate.Core.Utils;
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
        }
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

    public Task StopAsync()
    {
        foreach (var tcpServer in _tcpServers)
        {
            _logger.Information(
                "Stopping TCP server on {Address}:{Port}",
                tcpServer.Endpoint.Address,
                tcpServer.Endpoint.Port
            );
            tcpServer.StopAsync(default);
        }

        return Task.CompletedTask;
    }
}
