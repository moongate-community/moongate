using System.Text;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     What an item looks like to a player, resolved from the item, its template and the client files.
/// </summary>
public static class ItemEntityExtensions
{
    /// <summary>
    ///     Gets the name a player sees: the item's own name, else its template's, else the client's tiledata name of its
    ///     graphic in the singular or plural for its amount, else the template id.
    /// </summary>
    /// <remarks>
    ///     The entity keeps its name null unless it was given one: the client shows default names from its own files, and
    ///     a null name marks an item that was never renamed.
    /// </remarks>
    public static string DisplayName(this ItemEntity item, IItemTemplateService templates, ITileDataService tiles)
    {
        if (!string.IsNullOrWhiteSpace(item.Name))
        {
            return item.Name;
        }

        if (templates.TryGet(item.TemplateId, out var template) && !string.IsNullOrWhiteSpace(template.Name))
        {
            return template.Name;
        }

        if (tiles.TryGetItem(item.ItemId, out var tile) && !string.IsNullOrWhiteSpace(tile.Name))
        {
            return ResolvePlural(tile.Name, item.Amount > 1);
        }

        return item.TemplateId;
    }

    /// <summary>
    ///     Resolves the client's plural markers: <c>%s%</c> adds a plural ending ( <c>gold coin%s%</c>), and
    ///     <c>%plural/singular%</c> swaps one ending for the other ( <c>loa%ves/f%</c>).
    /// </summary>
    private static string ResolvePlural(string name, bool plural)
    {
        if (!name.Contains('%', StringComparison.Ordinal))
        {
            return name;
        }

        var parts = name.Split('%');
        var builder = new StringBuilder(name.Length);

        for (var i = 0; i < parts.Length; i++)
        {
            // Even parts are plain text; odd parts are the markers between a pair of '%'.
            if (i % 2 == 0)
            {
                builder.Append(parts[i]);

                continue;
            }

            var forms = parts[i].Split('/', 2);
            builder.Append(
                plural ? forms[0] :
                forms.Length > 1 ? forms[1] : ""
            );
        }

        return builder.ToString().Trim();
    }
}
