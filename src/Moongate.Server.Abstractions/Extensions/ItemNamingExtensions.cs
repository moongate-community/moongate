using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Localization;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Abstractions.Extensions;

/// <summary>
/// The one place that answers "what is this item called" for server-side surfaces — an admin list, a
/// Lua field, a log line. Not for the game client, which is sent the cliloc number and renders the
/// word itself, in the player's language and with its own plural.
/// </summary>
public static class ItemNamingExtensions
{
    /// <summary>
    /// The name to show for <paramref name="item" />: whatever it was renamed to, else what its
    /// template calls it, else the client's own word for that graphic, else the template id.
    /// <para>
    /// The cliloc step is the one that matters and the one easy to leave out: UO names almost
    /// everything by graphic, not by stored text, so an item nobody renamed has an empty
    /// <see cref="ItemEntity.Name" /> and is not nameless at all. Skipping it is how a pair of pants
    /// ends up reported as unnamed.
    /// </para>
    /// </summary>
    public static string DisplayName(this IClilocService clilocs, ItemEntity item, ItemTemplate? template)
    {
        if (item.Name.Length > 0)
        {
            return item.Name;
        }

        if (template?.Name is { Length: > 0 } templateName)
        {
            return templateName;
        }

        return clilocs.Text(ItemClilocs.ForItemId(item.ItemId)) ?? item.TemplateId;
    }
}
