using Moongate.Core.Extensions.Directories;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
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

        LoadTileData();

        return Task.CompletedTask;
    }

    // Movement, line of sight and item properties all read the tile flags, so a client without them cannot run a shard.
    private void LoadTileData()
    {
        if (Files.GetFilePath("tiledata.mul") is null)
        {
            throw new FileNotFoundException(
                $"tiledata.mul not found in the Ultima path: {_serverConfig.Ultima.UltimaPath}"
            );
        }

        // Reload even when TileData was touched before the client directory was set, which left it empty.
        TileData.Initialize();

        _logger.Information(
            "Loaded tiledata.mul: {LandCount} land tiles, {ItemCount} item tiles",
            TileData.LandTable.Length,
            TileData.ItemTable.Length
        );
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
