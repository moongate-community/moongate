namespace Moongate.UO.Data.Titles;

/// <summary>
/// A karma tier within a fame group: the upper karma threshold and the noble-title format string.
/// The format uses <c>{0}</c> for the character name and <c>{1}</c> for "Lord"/"Lady".
/// </summary>
public sealed class KarmaTitle
{
    public int Karma { get; set; }
    public string Title { get; set; } = string.Empty;
}
