using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Races;

/// <summary>
///     One race of <c>races.toml</c>, with what each of its genders gets.
/// </summary>
public class RaceContent
{
    public RaceType Race { get; set; }

    public string Name { get; set; }

    public RaceGenderContent Male { get; set; }

    public RaceGenderContent Female { get; set; }

    /// <summary>
    ///     Gets what <paramref name="gender" /> of this race gets.
    /// </summary>
    public RaceGenderContent For(GenderType gender)
    {
        return gender == GenderType.Female ? Female : Male;
    }
}
