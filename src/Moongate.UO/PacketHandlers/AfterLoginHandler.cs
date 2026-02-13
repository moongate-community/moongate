using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Events.Characters;
using Moongate.UO.Data.Events.Features;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Effects;
using Moongate.UO.Data.Packets.Environment;
using Moongate.UO.Data.Packets.GeneralInformation.Factory;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Lights;
using Moongate.UO.Data.Packets.Login;
using Moongate.UO.Data.Packets.System;
using Moongate.UO.Data.Packets.World;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class AfterLoginHandler : IMoongateService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IEventBusService _eventBusService;

    private readonly ISpatialWorldService _spatialWorldService;

    private readonly ILogger _logger = Log.ForContext<AfterLoginHandler>();

    public AfterLoginHandler(
        IGameSessionService gameSessionService,
        IEventBusService eventBusService,
        IMobileService mobileService,
        ISpatialWorldService spatialWorldService
    )
    {
        _gameSessionService = gameSessionService;
        _eventBusService = eventBusService;
        _spatialWorldService = spatialWorldService;
        _eventBusService.Subscribe<CharacterLoggedEvent>(OnCharacterLogged);
    }

    public void Dispose() { }

    private async Task OnCharacterLogged(CharacterLoggedEvent @event)
    {
        var session = _gameSessionService.GetSession(@event.SessionId);

        session.SendPackets(new ClientVersionPacket());
        session.SendPackets(new LoginConfigPacket(session.Mobile));

        session.SendPackets(new SupportFeaturesPacket());
        session.SendPackets(new DrawGamePlayerPacket(session.Mobile));

        session.SendPackets(new MobileDrawPacket(session.Mobile, session.Mobile, true, true));

        // Send worn items (spec PolServer: multiple items = multiple 0x2E packets)
        foreach (var equipment in session.Mobile.Equipment)
        {
            if (equipment.Key != ItemLayerType.Backpack && equipment.Key != ItemLayerType.Bank)
            {
                session.SendPackets(new WornItemPacket(session.Mobile, equipment.Value, equipment.Key));
            }
        }

        session.SendPackets(new DrawContainerAndAddItemCombinedPacket(session.Mobile.GetBackpack()));

        session.SendPackets(new WarModePacket(session.Mobile));
        session.SendPackets(GeneralInformationFactory.CreateSetCursorHueSetMap(session.Mobile.Map));
        session.SendPackets(new OverallLightLevelPacket(LightLevelType.Day));
        session.SendPackets(new PersonalLightLevelPacket(LightLevelType.Day, session.Mobile));
        session.SendPackets(new SeasonPacket(session.Mobile.Map.Season));

        var keys = new List<uint>();

        for (uint i = 0; i < 6; i++)
        {
            keys.Add(i + 2);
        }

        var fastWalk = GeneralInformationFactory.CreateInitializeFastWalkPrevention(keys.ToArray());

        session.SendPackets(fastWalk);

        session.SendPackets(new LoginCompletePacket());

        session.SendPackets(new SetTimePacket());
        session.SendPackets(new SeasonPacket(session.Mobile.Map.Season));
        session.SendPackets(GeneralInformationFactory.CreateSetCursorHueSetMap(session.Mobile.Map));

        var music = _spatialWorldService.GetMusicFromLocation(session.Mobile.Location, session.Mobile.Map.MapID);

        session.SendPackets(new SetMusicPacket(music));

        session.SendPackets(new PaperdollPacket(session.Mobile));

        MoongateContext.EnqueueAction(
            "AfterLoginHandler.OnCharacterLogged",
            async () =>
            {
                await Task.Delay(3000);
                await _eventBusService.PublishAsync(new CharacterInGameEvent(session, session.Mobile));
            }
        );
    }
}
