using System.Globalization;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Core.Extensions;

/// <summary>
///     Reads a server text with an English fallback, for code that must work with or without localization.
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    ///     Gets message <paramref name="id" /> in the server language with <paramref name="values" /> filled in; the
    ///     English text when the message files lack it or when no localization is registered (the login role).
    /// </summary>
    public static string Text(this ILocalizationService? localization, int id, string english, params object[] values)
    {
        return localization is not null && localization.TryGetText(id, out _)
            ? localization.Get(id, values)
            : string.Format(CultureInfo.InvariantCulture, english, values);
    }
}
