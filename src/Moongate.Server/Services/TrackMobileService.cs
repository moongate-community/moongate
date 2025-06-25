using System.Collections.Concurrent;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class TrackMobileService : ITrackMobileService
{
    private readonly ILogger _logger = Log.ForContext<TrackMobileService>();

    private readonly IGameSessionService _gameSessionService;

    private readonly ConcurrentDictionary<Serial, UOMobileEntity> _playerMobiles = new();

    private readonly ConcurrentDictionary<Serial, UOMobileEntity> _mobiles = new();

    public TrackMobileService(IGameSessionService gameSessionService)
    {
        _gameSessionService = gameSessionService;

        _gameSessionService.GameSessionBeforeDestroy += OnGameSessionBeforeDestroy;
    }

    private void OnGameSessionBeforeDestroy(GameSession session)
    {
        if (session.Mobile != null)
        {
            if (_playerMobiles.TryGetValue(session.Mobile.Id, out var mobile))
            {
                _playerMobiles.TryRemove(session.Mobile.Id, out _);
                _logger.Debug("Removed player mobile {MobileSerial} from tracking.", session.Mobile.Id);
            }
        }
    }


    public void Dispose()
    {
    }

    public void Track(UOMobileEntity mobile, bool isPlayer = false)
    {
        if (isPlayer)
        {
            if (_playerMobiles.TryAdd(mobile.Id, mobile))
            {
                _logger.Debug("Tracking player mobile {MobileSerial}.", mobile.Id);
            }
        }
        else
        {
            if (_mobiles.TryAdd(mobile.Id, mobile))
            {
                _logger.Debug("Tracking non-player mobile {MobileSerial}.", mobile.Id);
            }
        }
    }
}
