using Moongate.Core.Geometry;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Interfaces.World;

namespace Moongate.Server.Services.Mobiles;

/// <summary>Default <see cref="IMobileFactoryService" />: maps protocol input into mobile entities.</summary>
public sealed class MobileFactoryService : IMobileFactoryService
{
    private readonly IStartingCityService _startingCityService;

    public MobileFactoryService(IStartingCityService startingCityService)
    {
        _startingCityService = startingCityService;
    }

    public MobileEntity CreatePlayerMobile(CharacterCreationPacket packet)
    {
        var character = new MobileEntity
        {
            Name = packet.Name,
            Gender = packet.Gender,
            Race = packet.Race,
            ProfessionId = packet.ProfessionId,
            Strength = packet.Strength,
            Dexterity = packet.Dexterity,
            Intelligence = packet.Intelligence,
            SkinHue = new((ushort)packet.SkinHue),
            HairStyle = (ushort)packet.HairStyle,
            HairHue = new((ushort)packet.HairHue),
            FacialHairStyle = (ushort)packet.FacialHairStyle,
            FacialHairHue = new((ushort)packet.FacialHairHue)
        };

        foreach (var skill in packet.Skills)
        {
            if (skill.Value == 0)
            {
                continue; // unused starting-skill slot
            }

            character.Skills[skill.SkillId] = skill.Value * 10; // stored in tenths (50 -> 500)
        }

        // Fall back to the first city when the client sends an out-of-range index.
        var startingCity = _startingCityService.GetByIndex(packet.StartingCityIndex) ?? _startingCityService.GetByIndex(0);

        if (startingCity is not null)
        {
            character.MapId = (int)startingCity.Map;
            character.Position = new(startingCity.X, startingCity.Y, startingCity.Z);
        }

        return character;
    }

    public MobileEntity Create(string name, int mapId, Point3D position)
    {
        return new MobileEntity
        {
            Name = name,
            MapId = mapId,
            Position = position
        };
    }
}
