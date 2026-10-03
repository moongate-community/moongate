namespace Moongate.Server.Ultima.Data.Locations;

/// <summary>
///     One level of the named places: all of them, a map or a category, with the categories and the places directly
///     in it.
/// </summary>
public class LocationNode
{
    /// <summary>
    ///     The path from the top, joined by <c>/</c>, such as <c>Felucca/Dungeons/Covetous</c>; empty for the top.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    ///     The last name of the path; empty for the top.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    ///     The names of the categories directly in it, in file order; those of the top are the maps.
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    ///     The places directly in it, in file order.
    /// </summary>
    public IReadOnlyList<NamedLocation> Locations { get; init; } = [];
}
