using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Outgoing.Movement;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener,
 RegisterPacketHandler(PacketDefinition.CharacterCreationPacket),
 RegisterPacketHandler(PacketDefinition.RequestWarModePacket)
]

/// <summary>
/// Represents CharacterHandler.
/// </summary>
public class CharacterHandler : BasePacketListener, IGameEventListener<CharacterSelectedEvent>
{
    private readonly ILogger _logger = Log.ForContext<CharacterHandler>();
    private readonly ICharacterService _characterService;
    private readonly IEntityFactoryService _entityFactoryService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly ILightService? _lightService;

    public CharacterHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        ICharacterService characterService,
        IEntityFactoryService entityFactoryService,
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService spatialWorldService,
        ILightService? lightService = null
    ) : base(outgoingPacketQueue)
    {
        _characterService = characterService;
        _entityFactoryService = entityFactoryService;
        _gameEventBusService = gameEventBusService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _lightService = lightService;

        _gameEventBusService.RegisterListener(this);
    }

    public async Task HandleAsync(CharacterSelectedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var gameSession))
        {
            await HandleCharacterLoggedIn(gameSession, gameEvent.CharacterId);
        }
    }

    public async Task<bool> HandleCharacterLoggedIn(GameSession session, Serial characterId)
    {
        var character = await _characterService.GetCharacterAsync(characterId);

        if (character == null)
        {
            _logger.Error(
                "Failed to load character with ID {CharacterId} for session {SessionId}",
                characterId,
                session.SessionId
            );

            return false;
        }

        session.CharacterId = characterId;
        session.Character = character;
        session.MoveSequence = 0;
        session.SelfNotoriety = (byte)character.Notoriety;
        session.IsMounted = character.IsMounted;
        session.MoveCredit = 0;
        session.MoveTime = Environment.TickCount64;

        _logger.Information(
            "Character {CharacterName} (ID: {CharacterId}) logged in for session {SessionId}",
            character.Name,
            character.Id,
            session.SessionId
        );

        Enqueue(session, new ClientVersionPacket());
        Enqueue(session, new LoginConfirmPacket(character));
        Enqueue(session, new SupportFeaturesPacket());
        Enqueue(session, new DrawPlayerPacket(character));
        Enqueue(session, new MovementSpeedControlPacket(MovementSpeedControlType.Disable));
        Enqueue(session, new PlayerStatusPacket(character, 1));

        Enqueue(session, new MobileDrawPacket(character, character, true, true));
        WornItemPacketHelper.EnqueueVisibleWornItems(character, packet => Enqueue(session, packet));
        await EnqueueBackpackAsync(session, character);

        Enqueue(session, new WarModePacket(character));
        Enqueue(session, GeneralInformationPacket.CreateSetCursorHueSetMap(character.Map));
        var globalLight = _lightService?.ComputeGlobalLightLevel(character.MapId, character.Location) ?? (int)LightLevelType.Day;
        var globalLightLevel = (LightLevelType)(byte)Math.Clamp(globalLight, 0, byte.MaxValue);
        var personalLightLevel = (LightLevelType)(byte)0;
        Enqueue(session, new OverallLightLevelPacket(globalLightLevel));
        Enqueue(session, new PersonalLightLevelPacket(personalLightLevel, character));
        Enqueue(session, new SeasonPacket(character.Map.Season));

        Enqueue(session, new LoginCompletePacket());

        Enqueue(session, new SetTimePacket());
        Enqueue(session, new SeasonPacket(character.Map.Season));

        Enqueue(session, GeneralInformationPacket.CreateSetCursorHueSetMap(character.Map));
        Enqueue(session, new PaperdollPacket(character));

        Enqueue(session, new SetMusicPacket(_spatialWorldService.GetMusic(character.MapId, character.Location)));

        await _gameEventBusService.PublishAsync(
            new PlayerCharacterLoggedInEvent(session.SessionId, session.AccountId, character.Id)
        );

        return true;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is CharacterCreationPacket characterCreationPacket)
        {
            return await HandleCharacterCreationPacketAsync(session, characterCreationPacket);
        }

        if (packet is RequestWarModePacket requestWarModePacket)
        {
            return HandleRequestWarModeAsync(session, requestWarModePacket);
        }

        return true;
    }

    private bool HandleRequestWarModeAsync(GameSession session, RequestWarModePacket requestWarModePacket)
    {
        if (session.Character is null)
        {
            return true;
        }

        session.Character.IsWarMode = requestWarModePacket.IsWarMode;
        Enqueue(session, new WarModePacket(session.Character));

        return true;
    }

    private async Task EnqueueBackpackAsync(GameSession session, UOMobileEntity character)
    {
        var backpack = await _characterService.GetBackpackWithItemsAsync(character);

        if (backpack is null)
        {
            return;
        }

        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(backpack));
    }

    private async Task<bool> HandleCharacterCreationPacketAsync(
        GameSession session,
        CharacterCreationPacket characterCreationPacket
    )
    {
        var entity = _entityFactoryService.CreatePlayerMobile(characterCreationPacket, session.AccountId);

        entity.Title = "The creator of moongate";
        var newCharacter = await _characterService.CreateCharacterAsync(entity);
        await _characterService.ApplyStarterEquipmentHuesAsync(
            newCharacter,
            characterCreationPacket.Shirt.Hue,
            characterCreationPacket.Pants.Hue
        );

        await _characterService.AddCharacterToAccountAsync(session.AccountId, newCharacter);

        await HandleCharacterLoggedIn(session, newCharacter);

        return true;
    }

}
