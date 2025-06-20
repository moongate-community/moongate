using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Events.Characters;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class AfterLoginHandler : IMoongateService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IEventBusService _eventBusService;

    private readonly IMobileService _mobileService;

    private readonly ILogger _logger = Log.ForContext<AfterLoginHandler>();

    public AfterLoginHandler(IGameSessionService gameSessionService, IEventBusService eventBusService, IMobileService mobileService)
    {
        _gameSessionService = gameSessionService;
        _eventBusService = eventBusService;
        _mobileService = mobileService;
        _eventBusService.Subscribe<CharacterLoggedEvent>(OnCharacterLogged);
    }

    private async Task OnCharacterLogged(CharacterLoggedEvent @event)
    {
        var gameSession = _gameSessionService.GetSession(@event.SessionId);
    }


    public void Dispose()
    {
    }

}
