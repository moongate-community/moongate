using System.Globalization;
using Moongate.Http.Plugin.Data.Api.Characters;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Mobiles;

namespace Moongate.Http.Plugin.Services.Characters;

/// <summary>
/// Turns a mobile's skill dictionary into something a reader can use. The entity stores tenths under a
/// bare id, which is right for the wire and wrong for anyone looking at it: this names each skill from
/// the registry and reports points.
/// </summary>
public sealed class CharacterSkillReader
{
    private readonly ISkillService _skills;

    public CharacterSkillReader(ISkillService skills)
    {
        _skills = skills;
    }

    /// <summary>
    /// The skills the character has, by name. The dictionary is sparse — a starting-skill slot left at
    /// zero is never written — so this reports what is there rather than padding out the catalogue.
    /// </summary>
    public IReadOnlyList<CharacterSkillResponse> Read(MobileEntity mobile)
        => [.. mobile.Skills
                     .Select(entry => Describe(entry.Key, entry.Value))
                     .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)];

    private CharacterSkillResponse Describe(int id, MobileSkill skill)
    {
        // The catalogue is data and can lag the world, so a character may hold a skill nothing defines.
        // Its id reads worse than a name and far better than a blank row.
        var name = _skills.GetById(id)?.Name ?? id.ToString(CultureInfo.InvariantCulture);

        return new(id, name, skill.Value / 10.0, skill.Cap / 10.0, skill.Lock.ToString());
    }
}
