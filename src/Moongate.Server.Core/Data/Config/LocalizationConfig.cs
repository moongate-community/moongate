namespace Moongate.Server.Core.Data.Config;

/// <summary>
///     TOML settings for the language of the texts the server sends to players.
/// </summary>
public sealed class LocalizationConfig
{
    /// <summary>
    ///     Gets or sets the language code, such as <c>eng</c> or <c>ita</c>: the server reads
    ///     <c>data/messages/&lt;language&gt;.toml</c>.
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    ///     Validates the section before server services begin startup: the language must be a code of ASCII letters, as it
    ///     names a file.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(Language) || !Language.All(char.IsAsciiLetter))
        {
            throw new InvalidOperationException(
                $"The localization language must be a code of letters, such as eng or ita, found '{Language}'."
            );
        }
    }
}
