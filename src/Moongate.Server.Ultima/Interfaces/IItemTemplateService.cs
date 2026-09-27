using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Items;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Serves the item templates of <c>templates/items/</c>, with <c>base_id</c> already resolved.
/// </summary>
public interface IItemTemplateService
{
    /// <summary>
    ///     Gets how many templates were loaded.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets the template with id <paramref name="id" />, matching case.
    /// </summary>
    bool TryGet(string id, [NotNullWhen(true)] out ItemTemplate? template);

    /// <summary>
    ///     Gets the template with id <paramref name="id" />, matching case.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No template has that id.</exception>
    ItemTemplate Get(string id);
}
