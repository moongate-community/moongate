namespace Moongate.Server.Ultima.Data.Races;

/// <summary>
///     What one gender of a race of <c>races.toml</c> gets: its body and the hair and beard styles it may pick.
/// </summary>
public class RaceGenderContent
{
    /// <summary>
    ///     The body id of a living character of this race and gender.
    /// </summary>
    public int Body { get; set; }

    /// <summary>
    ///     The item ids of the allowed hair styles; no hair (0) is always allowed and is not listed.
    /// </summary>
    public List<int> Hair { get; set; } = [];

    /// <summary>
    ///     The item ids of the allowed beard styles; no beard (0) is always allowed and is not listed.
    /// </summary>
    public List<int> Beard { get; set; } = [];

    /// <summary>
    ///     Returns whether <paramref name="style" /> is a hair style this gender may pick; 0 (no hair) always is.
    /// </summary>
    public bool IsValidHair(int style)
    {
        return style == 0 || Hair.Contains(style);
    }

    /// <summary>
    ///     Returns whether <paramref name="style" /> is a beard style this gender may pick; 0 (no beard) always is.
    /// </summary>
    public bool IsValidBeard(int style)
    {
        return style == 0 || Beard.Contains(style);
    }
}
