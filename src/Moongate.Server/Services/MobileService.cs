using Moongate.Core.Persistence.Interfaces.Services;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;
using ZLinq;

namespace Moongate.Server.Services;

public class MobileService : IMobileService
{
    public event IMobileService.MobileEventHandler? MobileCreated;
    public event IMobileService.MobileEventHandler? MobileRemoved;
    public event IMobileService.MobileEventHandler? MobileAdded;
    public event IMobileService.MobileMovedEventHandler? MobileMoved;

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private readonly ILogger _logger = Log.ForContext<MobileService>();

    private const string mobilesFilePath = "mobiles.mga";

    private readonly Dictionary<Serial, UOMobileEntity> _availableMobiles = new();

    private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();

    private readonly IEntityFileService _entityFileService;

    public MobileService(IEntityFileService entityFileService)
    {
        _entityFileService = entityFileService;
    }

    private async Task SaveMobilesAsync()

    {
        await _saveLock.WaitAsync();
        await _entityFileService.SaveEntitiesAsync(mobilesFilePath, _availableMobiles.Values);
        _saveLock.Release();
    }

    private async Task LoadMobilesAsync()
    {
        await _saveLock.WaitAsync();
        _availableMobiles.Clear();

        var mobiles = await _entityFileService.LoadEntitiesAsync<UOMobileEntity>(mobilesFilePath);

        foreach (var mobile in mobiles)
        {
            AddInWorld(mobile);
        }

        _saveLock.Release();
    }


    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Loading mobiles from file...");

        return LoadMobilesAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Saving {Count} mobiles to file...", _availableMobiles.Count);
        return SaveMobilesAsync();
    }


    public UOMobileEntity CreateMobile()
    {
        _saveLock.Wait();
        var lastSerial = new Serial(Serial.MaxMobileSerial);

        if (_availableMobiles.Count > 0)
        {
            lastSerial = _availableMobiles.Keys.Last() + 1;
        }

        var mobile = new UOMobileEntity
        {
            Id = lastSerial,
        };


        _availableMobiles[lastSerial] = mobile;

        _saveLock.Release();

        return mobile;
    }

    public UOMobileEntity? GetMobile(Serial id)
    {
        if (_availableMobiles.TryGetValue(id, out var mobile))
        {
            return mobile;
        }

        _logger.Warning("Mobile with ID {Id} not found.", id);
        return null;
    }

    public IEnumerable<UOMobileEntity> QueryMobiles(Func<UOMobileEntity, bool> predicate)
    {
        return _availableMobiles.Values.AsValueEnumerable().Where(predicate).ToList();
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

    public void AddInWorld(UOMobileEntity mobile)
    {
        if (!_mobiles.TryAdd(mobile.Id, mobile))
        {
            _logger.Warning("Mobile with ID {Id} already exists.", mobile.Id);
            return;
        }

        MobileAdded?.Invoke(mobile);
        mobile.MobileMoved += MobileOnMobileMoved;
    }

    private void MobileOnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        _logger.Verbose("Mobile {Id} moved from {OldLocation} to {NewLocation}", mobile.Id, oldLocation, newLocation);
        MobileMoved?.Invoke(mobile, oldLocation, newLocation);
    }
}
