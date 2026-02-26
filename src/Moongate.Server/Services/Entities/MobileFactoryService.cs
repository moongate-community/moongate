using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Names;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Creates mobile entities from templates and character creation packets.
/// </summary>
public sealed class MobileFactoryService : IMobileFactoryService
{
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly INameService _nameService;
    private readonly IPersistenceService _persistenceService;

    public MobileFactoryService(
        IMobileTemplateService mobileTemplateService,
        INameService nameService,
        IPersistenceService persistenceService
    )
    {
        _mobileTemplateService = mobileTemplateService;
        _nameService = nameService;
        _persistenceService = persistenceService;
    }

    /// <inheritdoc />
    public UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileTemplateId);

        if (!_mobileTemplateService.TryGet(mobileTemplateId, out var template) || template is null)
        {
            throw new InvalidOperationException($"Mobile template '{mobileTemplateId}' not found.");
        }

        var now = DateTime.UtcNow;
        var generatedName = _nameService.GenerateName(template);

        var mobile = new UOMobileEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextMobileId(),
            AccountId = accountId ?? Serial.Zero,
            Name = string.IsNullOrWhiteSpace(generatedName) ? template.Name : generatedName,
            BaseBody = (Body)template.Body,
            Location = Point3D.Zero,
            Direction = DirectionType.South,
            IsPlayer = false,
            IsAlive = true,
            RaceIndex = 0,
            SkinHue = (short)template.SkinHue.Resolve(),
            HairStyle = (short)template.HairStyle,
            HairHue = (short)template.HairHue.Resolve(),
            Strength = template.Strength,
            Dexterity = template.Dexterity,
            Intelligence = template.Intelligence,
            Hits = template.Hits,
            Mana = template.Mana,
            Stamina = template.Stamina,
            Notoriety = template.Notoriety,
            CreatedUtc = now,
            LastLoginUtc = now
        };

        mobile.RecalculateMaxStats();

        if (template.MaxHits > 0)
        {
            mobile.MaxHits = template.MaxHits;
            mobile.Hits = Math.Min(mobile.Hits, mobile.MaxHits);
        }

        return mobile;
    }

    /// <inheritdoc />
    public UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var now = DateTime.UtcNow;
        var location = packet.StartingCity?.Location ?? Point3D.Zero;
        var mapId = packet.StartingCity?.Map?.Index ?? 0;

        var mobile = new UOMobileEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextMobileId(),
            AccountId = accountId,
            Name = packet.CharacterName,
            Location = location,
            MapId = mapId,
            Direction = DirectionType.South,
            IsPlayer = true,
            IsAlive = true,
            Gender = packet.Gender,
            RaceIndex = (byte)Math.Max(0, packet.RaceIndex),
            ProfessionId = packet.ProfessionId,
            SkinHue = packet.Skin.Hue,
            HairStyle = packet.Hair.Style,
            HairHue = packet.Hair.Hue,
            FacialHairStyle = packet.FacialHair.Style,
            FacialHairHue = packet.FacialHair.Hue,
            Strength = packet.Strength,
            Dexterity = packet.Dexterity,
            Intelligence = packet.Intelligence,
            Hits = packet.Strength,
            Mana = packet.Intelligence,
            Stamina = packet.Dexterity,
            IsWarMode = false,
            IsHidden = false,
            IsFrozen = false,
            IsPoisoned = false,
            IsBlessed = false,
            Notoriety = Notoriety.Innocent,
            CreatedUtc = now,
            LastLoginUtc = now
        };

        mobile.RecalculateMaxStats();

        return mobile;
    }

}
