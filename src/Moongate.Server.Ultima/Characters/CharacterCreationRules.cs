using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Characters;

/// <summary>
///     The checks applied to what a client asks for at character creation (packets 0xF8 and 0x8D). A value that is
///     not allowed is replaced with a safe one instead of refusing the character, the way established UO servers do.
/// </summary>
public static class CharacterCreationRules
{
    public const int StatTotal = 90;
    public const int MinStat = 10;
    public const int MaxStat = 60;
    public const int MaxStartingSkillValue = 50;
    public const string DefaultName = "Generic Player";

    private const int MinNameLength = 2;
    private const int MaxNameLength = 16;
    private const int HueMask = 0x3FFF;
    private const int MinDyedHue = 2;
    private const int MaxDyedHue = 1001;

    private const string NameSeparators = " -.'";

    private static readonly int[] AllowedSkillTotals = [100, 120];

    private static readonly HashSet<SkillType> NotStartingSkills =
    [
        SkillType.Stealth,
        SkillType.RemoveTrap,
        SkillType.Spellweaving
    ];



    /// <summary>
    ///     Keeps the stats when each is between <see cref="MinStat" /> and <see cref="MaxStat" /> and they add up to
    ///     <see cref="StatTotal" />; otherwise every stat becomes <see cref="MinStat" />.
    /// </summary>
    public static (int Strength, int Dexterity, int Intelligence) ValidateStats(
        int strength,
        int dexterity,
        int intelligence
    )
    {
        var valid = IsStat(strength) &&
                    IsStat(dexterity) &&
                    IsStat(intelligence) &&
                    strength + dexterity + intelligence == StatTotal;

        return valid ? (strength, dexterity, intelligence) : (MinStat, MinStat, MinStat);
    }

    /// <summary>
    ///     Checks the starting skills. They are valid when each value is at most
    ///     <see cref="MaxStartingSkillValue" />, every skill may be chosen at creation, no skill with a value appears
    ///     twice and the values add up to 100 or 120. A skill the race cannot start with (throwing for any race but
    ///     gargoyles, archery for gargoyles) is dropped without making the choice invalid.
    /// </summary>
    /// <returns>
    ///     The skills to give, without the dropped ones and those at 0; empty when the choice is not valid.
    /// </returns>
    public static IReadOnlyList<CharacterSkillChoice> ValidateSkills(
        IReadOnlyList<CharacterSkillChoice> skills,
        RaceType race
    )
    {
        var accepted = new List<CharacterSkillChoice>(skills.Count);
        var total = 0;

        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];

            if (skill.Value > MaxStartingSkillValue ||
                !Enum.IsDefined(skill.Skill) ||
                NotStartingSkills.Contains(skill.Skill))
            {
                return [];
            }

            for (var next = index + 1; next < skills.Count; next++)
            {
                if (skills[next].Value > 0 && skills[next].Skill == skill.Skill)
                {
                    return [];
                }
            }

            total += skill.Value;

            if (skill.Value > 0 && IsAllowedForRace(skill.Skill, race))
            {
                accepted.Add(skill);
            }
        }

        return AllowedSkillTotals.Contains(total) ? accepted : [];
    }

    /// <summary>
    ///     Keeps a name of <see cref="MinNameLength" /> to <see cref="MaxNameLength" /> letters that may contain a space,
    ///     dash, period or apostrophe, but not at the start and never two in a row, and uses no banned word;
    ///     otherwise returns <see cref="DefaultName" />.
    /// </summary>
    public static string ValidateName(string? name, BannedNamesContent bannedNames)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        return IsValidName(trimmed) && !bannedNames.IsBanned(trimmed, NameSeparators.ToCharArray())
            ? trimmed
            : DefaultName;
    }

    /// <summary>
    ///     Clips a skin hue to the race and marks it as partial, so the client colors only the gray pixels of the body.
    /// </summary>
    public static Hue ValidateSkinHue(RaceContent race, Hue hue)
    {
        return new((ushort)(race.ClipSkinHue(hue).Value | Hue.PartialFlag));
    }

    /// <summary>
    ///     Keeps a hair style the race and gender may pick, with its hue clipped to the race; any other style, or no
    ///     hair, gives no hair.
    /// </summary>
    public static (int Style, Hue Hue) ValidateHair(RaceContent race, GenderType gender, int style, Hue hue)
    {
        return style != 0 && race.For(gender).IsValidHair(style) ? (style, race.ClipHairHue(hue)) : (0, Hue.None);
    }

    /// <summary>
    ///     Keeps a beard style the race and gender may pick, with its hue clipped to the race; any other style, or no
    ///     beard, gives no beard.
    /// </summary>
    public static (int Style, Hue Hue) ValidateBeard(RaceContent race, GenderType gender, int style, Hue hue)
    {
        return style != 0 && race.For(gender).IsValidBeard(style) ? (style, race.ClipHairHue(hue)) : (0, Hue.None);
    }

    /// <summary>
    ///     Clips the hue of a starting shirt or pants to the dyeable range, 2 to 1001.
    /// </summary>
    public static Hue ValidateClothingHue(Hue hue)
    {
        return new((ushort)Math.Clamp(hue.Value & HueMask, MinDyedHue, MaxDyedHue));
    }

    private static bool IsStat(int value)
    {
        return value is >= MinStat and <= MaxStat;
    }

    private static bool IsAllowedForRace(SkillType skill, RaceType race)
    {
        return skill switch
        {
            SkillType.Throwing => race == RaceType.Gargoyle,
            SkillType.Archery => race != RaceType.Gargoyle,
            _ => true
        };
    }

    private static bool IsValidName(string name)
    {
        if (name.Length is < MinNameLength or > MaxNameLength || NameSeparators.Contains(name[0]))
        {
            return false;
        }

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            var isSeparator = NameSeparators.Contains(character);

            if (!isSeparator && !char.IsAsciiLetter(character))
            {
                return false;
            }

            if (isSeparator && NameSeparators.Contains(name[index - 1]))
            {
                return false;
            }
        }

        return true;
    }
}
