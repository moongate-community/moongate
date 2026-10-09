using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Whether a creature can be ridden, read from the tag of its mobile template.
/// </summary>
public static class MobileTemplateMountExtensions
{
    /// <summary>
    ///     Gets the id of the item the creature turns into when it is ridden; null when the template has no
    ///     <see cref="MountProps.MountItemTag" />, or an empty one.
    /// </summary>
    public static string? MountItem(this MobileTemplate template)
    {
        return template.Tags?.GetValueOrDefault(MountProps.MountItemTag) is { Length: > 0 } item ? item : null;
    }
}
