using Moongate.Core.Extensions.Directories;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Serilog;

namespace Moongate.Server.Services.Ultima;

public class UltimaDataService : IUltimaDataService
{
    private readonly UltimaConfig _config;
    private readonly ILogger _logger = Log.ForContext<UltimaDataService>();

    public UltimaDataService(UltimaConfig config)
    {
        _config = config;
    }

    public Task StartAsync()
    {
        _config.UltimaPath = _config.UltimaPath.ResolvePathAndEnvs();

        if (!Directory.Exists(_config.UltimaPath))
        {
            _logger.Error("Ultima path does not exist: {UltimaPath}", _config.UltimaPath);

            throw new DirectoryNotFoundException($"Ultima path does not exist: {_config.UltimaPath}");
        }

        Files.SetDirectory(_config.UltimaPath);

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
                $"tiledata.mul not found in the Ultima path: {_config.UltimaPath}"
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
