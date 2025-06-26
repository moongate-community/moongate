using System.ComponentModel;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Context;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;
using Serilog;

namespace Moongate.Server.Services;

public class NotificationSystem : INotificationSystem
{
    private readonly ILogger _logger = Log.ForContext<NotificationSystem>();

    private readonly IGameSessionService _gameSessionService;
    private readonly IMobileService _mobileService;
    private readonly IPlayerNotificationSystem _playerNotificationSystem;

    private readonly ICommandSystemService _commandSystemService;

    public NotificationSystem(
        IMobileService mobileService, IGameSessionService gameSessionService,
        IPlayerNotificationSystem playerNotificationSystem, ICommandSystemService commandSystemService
    )
    {
        _mobileService = mobileService;
        _gameSessionService = gameSessionService;
        _playerNotificationSystem = playerNotificationSystem;
        _commandSystemService = commandSystemService;

        _gameSessionService.GameSessionCreated += OnGameSessionCreated;
        _gameSessionService.GameSessionBeforeDestroy += OnGameSessionBeforeDestroy;
    }

    private void OnGameSessionBeforeDestroy(GameSession session)
    {
        session.MobileChanged -= OnGameSessionMobileChanged;
        _playerNotificationSystem.UntrackMobile(session.Mobile);
    }

    private void OnGameSessionCreated(GameSession session)
    {
        session.MobileChanged += OnGameSessionMobileChanged;
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

    private async Task HandleCommandAsync(UOMobileEntity mobile, string command)
    {
        _logger.Debug("Handling command '{Command}' for mobile {MobileId}", command, mobile.Id);

        var gameSession = _gameSessionService.QuerySessionFirstOrDefault(s => s.Mobile.Id == mobile.Id);
        await _commandSystemService.ExecuteCommandAsync(
            command,
            gameSession.SessionId,
            gameSession.Account.AccountLevel,
            CommandSourceType.InGame
        );

    }

    public void SendSystemMessageToAll(string message)
    {
        foreach (var other in _gameSessionService.QuerySessions(s => true).Select(s => s.Mobile))
        {
            // Log the system message
            _logger.Information("Sending system message to mobile {MobileId}: {Message}", other.Id, message);

            // Send the system message
            SendSystemMessageToMobile(other, message);
        }
    }

    public void SendSystemMessageToMobile(UOMobileEntity mobile, string message)
    {
        mobile.ReceiveSpeech(null, ChatMessageType.System, 0, message, 0, 3);
    }

    public async Task SendChatMessageAsync(
        UOMobileEntity mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        // check if command
        if (text.StartsWith("."))
        {
            await HandleCommandAsync(mobile, text[1..]);
            return;
        }

        var nonPlayersToNotify =
            _mobileService.QueryMobiles(m => m.Location.InRange(mobile.Location, UOContext.LineOfSight)).ToList();

        foreach (var other in nonPlayersToNotify)
        {
            if (other.Id == mobile.Id)
            {
                mobile.Speech(messageType, hue, text, graphic, font);
            }

            other.ReceiveSpeech(mobile, messageType, hue, text, graphic, font);
        }
    }
}
