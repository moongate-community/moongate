using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class CharactersHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<CharactersHandler>();

    private readonly IMobileService _mobileService;
    private readonly IAccountService _accountService;

    public CharactersHandler(IMobileService mobileService, IAccountService accountService)
    {
        _mobileService = mobileService;
        _accountService = accountService;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is CharacterCreationPacket characterCreation)
        {
            await CreateCharacterAsync(session, characterCreation);
        }
    }

    private async Task CreateCharacterAsync(
        GameSession session, CharacterCreationPacket characterCreation
    )
    {
        _logger.Information(
            "Creating character for account {AccountName} character: {Character}",
            session.Account.Username,
            characterCreation.CharacterName
        );

        var playerMobileEntity = _mobileService.CreateMobile();

        session.Account.Characters.Add(new UOAccountCharacterEntity()
        {
            MobileId = playerMobileEntity.Id,
            Name = characterCreation.CharacterName,
            Slot = session.Account.Characters.Count + 1,

        });

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



        foreach (var skill in characterCreation.Skills)
        {
            playerMobileEntity.Skills.Add(skill.Skill, skill.Value);
        }

        playerMobileEntity.Profession = characterCreation.Profession;

        playerMobileEntity.RecalculateMaxStats();





    }
}
