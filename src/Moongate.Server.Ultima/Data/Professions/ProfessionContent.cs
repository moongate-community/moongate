namespace Moongate.Server.Ultima.Data.Professions;

/// <summary>
///     One profession of <c>professions.toml</c>: what the client shows and the skills and stats a character
///     created with it starts with.
/// </summary>
public class ProfessionContent
{
    /// <summary>
    ///     The profession id the client sends at character creation; 0 is the "Advanced" choice and is never listed.
    /// </summary>
    public int Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    ///     The id of the localized name the client shows.
    /// </summary>
    public int NameCliloc { get; set; }

    /// <summary>
    ///     The id of the localized description the client shows.
    /// </summary>
    public int DescriptionCliloc { get; set; }

    /// <summary>
    ///     The id of the gump image the client shows.
    /// </summary>
    public int Gump { get; set; }

    public int Str { get; set; }

    public int Dex { get; set; }

    public int Int { get; set; }

    public List<ProfessionSkillContent> Skills { get; set; } = [];
}
