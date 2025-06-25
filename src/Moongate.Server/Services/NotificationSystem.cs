using System.ComponentModel;
using Moongate.UO.Context;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;

namespace Moongate.Server.Services;

public class NotificationSystem : INotificationSystem
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IMobileService _mobileService;
    private readonly IPlayerNotificationSystem _playerNotificationSystem;

    public NotificationSystem(
        IMobileService mobileService, IGameSessionService gameSessionService,
        IPlayerNotificationSystem playerNotificationSystem
    )
    {
        _mobileService = mobileService;
        _gameSessionService = gameSessionService;
        _playerNotificationSystem = playerNotificationSystem;

        _gameSessionService.GameSessionCreated += OnGameSessionCreated;
        _gameSessionService.GameSessionBeforeDestroy += OnGameSessionBeforeDestroy;
    }

    private void OnGameSessionBeforeDestroy(GameSession session)
    {
        session.MobileChanged -= OnGameSessionMobileChanged;
    }

    private void OnGameSessionCreated(GameSession session)
    {
        session.MobileChanged += OnGameSessionMobileChanged;
        _playerNotificationSystem.UntrackMobile(session.Mobile);
    }

    private void OnGameSessionMobileChanged(object sender, UOMobileEntity entity)
    {
        entity.PropertyChanged += OnMobilePropertyChanged;
        _playerNotificationSystem.TrackMobile(entity);
    }

    private void OnMobilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UOMobileEntity.Location))
        {
            if (sender is UOMobileEntity mobile)
            {
                HandleMobileMovement(mobile);
            }
        }
    }

    private void HandleMobileMovement(UOMobileEntity mobile)
    {
        var nonPlayersToNotify =
            _mobileService.QueryMobiles(m => m.Location.InRange(mobile.Location, UOContext.LineOfSight)).ToList();

        foreach (var other in nonPlayersToNotify)
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            other.OnOtherMobileMoved(mobile);
        }
    }


    public void Dispose()
    {
    }
}
