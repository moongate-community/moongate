using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class MobileService : IMobileService
{
    private readonly ILogger _logger = Log.ForContext<MobileService>();

    private const string mobilesFilePath = "mobiles.mga";

    private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();

    private readonly IEntityFileService _entityFileService;

    public MobileService(IEntityFileService entityFileService)
    {
        _entityFileService = entityFileService;
    }

    private Task SaveMobilesAsync()
    {
        return _entityFileService.SaveEntitiesAsync(mobilesFilePath, _mobiles.Values);
    }

    private async Task LoadMobilesAsync()
    {
        _mobiles.Clear();

        var mobiles = await _entityFileService.LoadEntitiesAsync<UOMobileEntity>(mobilesFilePath);

        foreach (var mobile in mobiles)
        {
            _mobiles[mobile.Id] = mobile;
        }
    }


    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Loading mobiles from file...");

        return LoadMobilesAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Saving {Count} mobiles to file...", _mobiles.Count);
        return SaveMobilesAsync();
    }

    public UOMobileEntity CreateMobile()
    {

        var lastSerial = new Serial(Serial.MaxMobileSerial);

        if (_mobiles.Count > 0)
        {
            lastSerial = _mobiles.Keys.Last() + 1;
        }

        var mobile = new UOMobileEntity
        {
            Id = lastSerial,
        };

        _mobiles[mobile.Id] = mobile;

        return mobile;

    }

    public UOMobileEntity? GetMobile(Serial id)
    {
        if (_mobiles.TryGetValue(id, out var mobile))
        {
            return mobile;
        }

        _logger.Warning("Mobile with ID {Id} not found.", id);
        return null;
    }


    public void Dispose()
    {
        // TODO release managed resources here
    }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return LoadMobilesAsync();
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return SaveMobilesAsync();
    }
}
