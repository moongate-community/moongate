using System.Diagnostics.CodeAnalysis;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives the texts of <c>data/messages</c> in the server's language, as set by <c>localization.language</c> in
///     <c>moongate.toml</c>.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    ///     Gets the language code of the texts, such as <c>eng</c> or <c>ita</c>.
    /// </summary>
    string Language { get; }

    /// <summary>
    ///     Gets message <paramref name="id" /> with <c>{0}</c>, <c>{1}</c>, ... replaced by <paramref name="values" /> and
    ///     <c>{{</c>, <c>}}</c> turned into single braces.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     No message has this id.
    /// </exception>
    /// <exception cref="FormatException">
    ///     The message needs more values than were given.
    /// </exception>
    string Get(int id, params object[] values);

    /// <summary>
    ///     Gets the text of message <paramref name="id" /> as written in the file, without filling in any value.
    /// </summary>
    /// <returns>
    ///     False when no message has this id.
    /// </returns>
    bool TryGetText(int id, [MaybeNullWhen(false)] out string text);
}
