using Moongate.UO.Data.Titles;

namespace Moongate.Server.Interfaces;

/// <summary>Fame/karma noble-title lookup table.</summary>
public interface ITitleService
{
    /// <summary>All fame groups in load order (ascending fame threshold).</summary>
    IReadOnlyList<FameTitleGroup> All { get; }

    /// <summary>Number of fame groups.</summary>
    int Count { get; }

    /// <summary>Adds a fame group to the table, preserving order.</summary>
    void Register(FameTitleGroup group);

    /// <summary>
    /// Returns the noble title for a character given its fame and karma. Picks the first fame tier whose
    /// threshold is not exceeded (else the last), then the first karma tier within it, and formats the
    /// title with the name and "Lord"/"Lady". Returns the bare name when the table is empty.
    /// </summary>
    string GetTitle(string name, int fame, int karma, bool female);
}
