namespace Moongate.Server.Ultima.Data.Names;

/// <summary>
///     One list of <c>data/names.toml</c>: the names a random NPC name is drawn from.
/// </summary>
public class NameList
{
    /// <summary>
    ///     The id a mobile template names the list by, such as <c>male</c>; unique ignoring case.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     The names; none is empty.
    /// </summary>
    public List<string> Names { get; set; } = [];
}
