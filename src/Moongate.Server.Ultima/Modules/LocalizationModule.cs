using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>localization</c> Lua module: the texts of <c>data/messages</c> in the server's language, through
///     <see cref="ILocalizationService" />.
/// </summary>
[ScriptModule("localization", "Gives the server texts in the server's language.")]
public sealed class LocalizationModule
{
    private readonly ILocalizationService _localizationService;

    public LocalizationModule(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    ///     Gets message <paramref name="id" /> with <c>{0}</c>, <c>{1}</c>, ... replaced by <paramref name="values" />;
    ///     scripts call it as <c>localization.get(id, ...)</c>. A whole Lua number is passed as an integer, so
    ///     <c>{0:x}</c> works on it.
    /// </summary>
    [ScriptFunction(helpText: "Returns message id with {0}, {1}, ... replaced by the extra arguments.")]
    public string Get(int id, params object?[] values)
    {
        var converted = values.Select(value => value is double number && double.IsInteger(number) && Math.Abs(number) <= int.MaxValue
                                                   ? (object)(long)number
                                                   : value ?? "nil")
                              .ToArray();

        return _localizationService.Get(id, converted);
    }

    /// <summary>
    ///     Gets the text of message <paramref name="id" /> as written in the file, or <c>nil</c> when no message has
    ///     this id; scripts call it as <c>localization.text(id)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Returns the text of message id without filling in values, or nil when it does not exist.")]
    public string? Text(int id)
    {
        return _localizationService.TryGetText(id, out var text) ? text : null;
    }

    /// <summary>
    ///     Gets the server's language code, such as <c>eng</c> or <c>ita</c>; scripts call it as
    ///     <c>localization.language()</c>.
    /// </summary>
    [ScriptFunction(helpText: "Returns the server's language code, such as eng or ita.")]
    public string Language()
    {
        return _localizationService.Language;
    }
}
