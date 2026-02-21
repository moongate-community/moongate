using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.Characters;
using Moongate.UO.Data.Events.Contexts;
using Moongate.UO.Data.Events.System;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class CharactersHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<CharactersHandler>();

    private readonly IMobileService _mobileService;
    private readonly IEventBusService _eventBusService;
    private readonly IScriptEngineService _scriptEngineService;

    private readonly IEntityFactoryService _entityFactoryService;

    public CharactersHandler(
        IMobileService mobileService,
        IEventBusService eventBusService,
        IScriptEngineService scriptEngineService,
        IEntityFactoryService entityFactoryService
    )
    {
        _mobileService = mobileService;
        _eventBusService = eventBusService;
        _scriptEngineService = scriptEngineService;
        _entityFactoryService = entityFactoryService;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is CharacterCreationPacket characterCreation)
        {
            await CreateCharacterAsync(session, characterCreation);

            return;
        }

        if (packet is CharacterDeletePacket characterDeletion)
        {
            await DeleteCharacterAsync(session, characterDeletion);

            return;
        }

        if (packet is CharacterLoginPacket characterLogin)
        {
            await SelectCharacterAsync(session, characterLogin);
        }
    }

    private async Task CreateCharacterAsync(GameSession session, CharacterCreationPacket characterCreation)
    {
        _logger.Information(
            "Creating character for account {AccountName} character: {Character}",
            session.Account.Username,
            characterCreation.CharacterName
        );

        var playerMobileEntity = await _mobileService.CreateMobileAsync();

        session.Account.Characters.Add(
            new()
            {
                MobileId = playerMobileEntity.Id,
                Name = characterCreation.CharacterName,
                Slot = session.Account.Characters.Count + 1
            }
        );

        playerMobileEntity.Name = characterCreation.CharacterName;
        playerMobileEntity.Created = DateTime.UtcNow;
        playerMobileEntity.Dexterity = characterCreation.Dexterity;
        playerMobileEntity.Strength = characterCreation.Strength;
        playerMobileEntity.Intelligence = characterCreation.Intelligence;
        playerMobileEntity.FacialHairHue = characterCreation.FacialHair.Hue;
        playerMobileEntity.FacialHairStyle = characterCreation.FacialHair.Style;
        playerMobileEntity.HairHue = characterCreation.Hair.Hue;
        playerMobileEntity.HairStyle = characterCreation.Hair.Style;
        playerMobileEntity.Location = characterCreation.StartingCity.Location;
        playerMobileEntity.SkinHue = characterCreation.Skin.Hue;

        playerMobileEntity.Gender = characterCreation.Gender;
        playerMobileEntity.Race = characterCreation.Race;

        playerMobileEntity.Map = characterCreation.StartingCity.Map;

        foreach (var skill in characterCreation.Skills)
        {
            playerMobileEntity.SetSkillValue(skill.Skill, skill.Value);
        }

        playerMobileEntity.Profession = characterCreation.Profession;

        playerMobileEntity.RecalculateMaxStats();

        playerMobileEntity.AddItem(ItemLayerType.Backpack, _entityFactoryService.GetNewBackpack());

        var goldItem = _entityFactoryService.CreateItemEntity("gold");

        goldItem.Amount = 1000;

        playerMobileEntity.GetBackpack().AddItem(goldItem, new(1, 1));

        playerMobileEntity.IsPlayer = true;

        var createContext =
            new CharacterCreatedEvent(
                session.Account.Username,
                playerMobileEntity,
                UoEventContext.CreateInstance()
            );

        await _eventBusService.PublishAsync(createContext);

        _scriptEngineService.ExecuteCallback("OnCharacterCreated", createContext);

        // Create default clothing items
        var shirtItem = _entityFactoryService.CreateItemEntity("shirt");
        shirtItem.Hue = characterCreation.Shirt.Hue;
        playerMobileEntity.AddItem(ItemLayerType.Shirt, shirtItem);

        var pantsItem = _entityFactoryService.CreateItemEntity("pants");
        pantsItem.Hue = characterCreation.Pants.Hue;
        playerMobileEntity.AddItem(ItemLayerType.Pants, pantsItem);

        var shoesItem = _entityFactoryService.CreateItemEntity("shoes");
        playerMobileEntity.AddItem(ItemLayerType.Shoes, shoesItem);

        session.Mobile = playerMobileEntity;

        _mobileService.AddInWorld(playerMobileEntity);

        await _eventBusService.PublishAsync(new SavePersistenceRequestEvent());

        await _eventBusService.PublishAsync(
            new CharacterLoggedEvent(
                session.SessionId,
                playerMobileEntity.Id,
                playerMobileEntity.Name
            )
        );
    }

    private async Task DeleteCharacterAsync(GameSession session, CharacterDeletePacket characterDeletion)
    {
        var character = session.Account.GetCharacter(characterDeletion.Index);

        if (character == null)
        {
            _logger.Warning(
                "Character deletion failed for account {AccountName} - Character at index {Index} not found.",
                session.Account.Username,
                characterDeletion.Index
            );

            return;
        }

        var mobileEntity = _mobileService.GetMobile(character.MobileId);

        if (mobileEntity == null)
        {
            _logger.Warning(
                "Character deletion failed for account {AccountName} character: {Character} - Mobile entity not found.",
                session.Account.Username,
                character.Name
            );

            return;
        }

        _logger.Information(
            "Deleting character for account {AccountName} character: {Character}",
            session.Account.Username,
            character.Name
        );

        session.Account.RemoveCharacter(character);

        await _eventBusService.PublishAsync(new SavePersistenceRequestEvent());

        var charactersAfterDelete = new CharacterAfterDeletePacket();
        charactersAfterDelete.FillCharacters(
            session.GetCharactersEntries().Count == 0
                ? null
                : session.GetCharactersEntries()
        );
    }

    private async Task SelectCharacterAsync(GameSession session, CharacterLoginPacket packet)
    {
        var character = session.Account.GetCharacter(packet.CharacterName);

        if (character == null)
        {
            _logger.Warning(
                "Character selection failed for account {AccountName} - Character {CharacterName} not found.",
                session.Account.Username,
                packet.CharacterName
            );

            return;
        }

        var mobile = _mobileService.GetMobile(character.MobileId);

        if (mobile == null)
        {
            _logger.Warning(
                "Character selection failed for account {AccountName} - Mobile entity for character {CharacterName} not found.",
                session.Account.Username,
                packet.CharacterName
            );

            return;
        }

        session.Mobile = mobile;
        mobile.IsPlayer = true;

        _mobileService.AddInWorld(mobile);

        await _eventBusService.PublishAsync(new CharacterLoggedEvent(session.SessionId, character.MobileId, character.Name));
    }
}
