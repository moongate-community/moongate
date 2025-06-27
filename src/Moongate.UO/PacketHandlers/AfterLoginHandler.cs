using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Events.Characters;
using Moongate.UO.Data.Events.Features;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Environment;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Lights;
using Moongate.UO.Data.Packets.Login;
using Moongate.UO.Data.Packets.Maps;
using Moongate.UO.Data.Packets.System;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class AfterLoginHandler : IMoongateService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IEventBusService _eventBusService;

    private readonly IMobileService _mobileService;


    private readonly ILogger _logger = Log.ForContext<AfterLoginHandler>();

    public AfterLoginHandler(
        IGameSessionService gameSessionService, IEventBusService eventBusService, IMobileService mobileService
    )
    {
        _gameSessionService = gameSessionService;
        _eventBusService = eventBusService;
        _mobileService = mobileService;
        _eventBusService.Subscribe<CharacterLoggedEvent>(OnCharacterLogged);
    }

    private async Task OnCharacterLogged(CharacterLoggedEvent @event)
    {
        var session = _gameSessionService.GetSession(@event.SessionId);


        session.SendPackets(new ClientVersionPacket());
        session.SendPackets(new LoginConfigPacket(session.Mobile));


        session.SendPackets(new SupportFeaturesPacket());
        session.SendPackets(new DrawGamePlayerPacket(session.Mobile));

        session.SendPackets(new MobileDrawPacket(session.Mobile, session.Mobile, true, true));


        session.SendPackets(new WornItemsPacket(session.Mobile));

        session.SendPackets(new WarModePacket(session.Mobile));
        session.SendPackets(new MapChangePacket(session.Mobile.Map));
        session.SendPackets(new OverallLightLevelPacket(LightLevelType.Day));
        session.SendPackets(new PersonalLightLevelPacket(LightLevelType.Day, session.Mobile));
        session.SendPackets(new SeasonPacket(session.Mobile.Map.Season));
        session.SendPackets(new LoginCompletePacket());

        MoongateContext.EnqueueAction(
            "AfterLoginHandler.OnCharacterLogged",
            async () =>
            {
                await Task.Delay(3000);
                await _eventBusService.PublishAsync(new CharacterInGameEvent(session, session.Mobile));
            }
        );
    }


    public void Dispose()
    {
    }
}
