namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Picks random NPC names from the lists in <c>data/names.toml</c>.
/// </summary>
public interface INameService
{
    /// <summary>
    ///     Gets whether a list has the id <paramref name="listId" />, ignoring case.
    /// </summary>
    bool HasList(string listId);

    /// <summary>
    ///     Picks a random name from the list <paramref name="listId" />, ignoring case.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No list has that id.</exception>
    string RandomName(string listId);
}
