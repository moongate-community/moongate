namespace Moongate.Server.Ultima.Data.Titles;

/// <summary>
///     One validated pair of inclusive fame and karma thresholds and its title prefixes.
/// </summary>
public sealed class FameKarmaTitle
{
    public int Fame { get; }

    public int Karma { get; }

    public string Title { get; }

    public string? FemaleTitle { get; }

    public FameKarmaTitle(int fame, int karma, string title, string? femaleTitle = null)
    {
        Fame = fame;
        Karma = karma;
        Title = title;
        FemaleTitle = femaleTitle;
    }
}
