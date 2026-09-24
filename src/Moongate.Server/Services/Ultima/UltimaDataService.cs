using Moongate.Core.Extensions.Directories;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Ultima.Io;
using Serilog;

namespace Moongate.Server.Services.Ultima;

public class UltimaDataService : IUltimaDataService
{
    private readonly MoongateServerConfig _serverConfig;
    private readonly ILogger _logger = Log.ForContext<UltimaDataService>();

    public UltimaDataService(MoongateServerConfig serverConfig)
    {
        _serverConfig = serverConfig;
    }

    public Task StartAsync()
    {
        _serverConfig.Ultima.UltimaPath = _serverConfig.Ultima.UltimaPath.ResolvePathAndEnvs();

        if (!Directory.Exists(_serverConfig.Ultima.UltimaPath))
        {
            _logger.Error("Ultima path does not exist: {UltimaPath}", _serverConfig.Ultima.UltimaPath);

            throw new DirectoryNotFoundException($"Ultima path does not exist: {_serverConfig.Ultima.UltimaPath}");
        }

        Files.SetDirectory(_serverConfig.Ultima.UltimaPath);

        var clientVersion = ClientVersionReader.Read();
        _logger.Information("Ultima client version: {ClientVersion}", clientVersion);

        return Task.CompletedTask;
    }

    public Task StopAsync()
        => Task.CompletedTask;
}
