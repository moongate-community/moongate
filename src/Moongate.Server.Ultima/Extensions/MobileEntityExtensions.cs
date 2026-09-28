using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     What a mobile looks like to a player.
/// </summary>
public static class MobileEntityExtensions
{
    /// <summary>
    ///     Gets the name a player sees: the name followed by the title when there is one (titles carry their article, as
    ///     in "Bob the Weaponsmith"). A mobile without a name falls back to its template id, then its serial.
    /// </summary>
    public static string DisplayName(this MobileEntity mobile)
    {
        var name = !string.IsNullOrWhiteSpace(mobile.Name) ? mobile.Name
            : !string.IsNullOrWhiteSpace(mobile.TemplateId) ? mobile.TemplateId
            : mobile.Id.ToString();

        return string.IsNullOrWhiteSpace(mobile.Title) ? name : $"{name} {mobile.Title}";
    }
}
