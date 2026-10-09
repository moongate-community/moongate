using Moongate.Server.Ultima.Data.Crafts;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Keeps the crafts of <c>data/crafts</c> and their resource lists, read only.
/// </summary>
public interface ICraftService
{
    /// <summary>
    ///     Gets the craft of an id, such as <c>carpentry</c>.
    /// </summary>
    /// <returns>
    ///     Null for an unknown id.
    /// </returns>
    CraftDefinition? Get(string id);

    /// <summary>
    ///     Gets the item templates that count for a resource list, such as <c>wood</c>.
    /// </summary>
    /// <returns>
    ///     Null for an unknown list.
    /// </returns>
    IReadOnlyList<string>? Resource(string id);
}
