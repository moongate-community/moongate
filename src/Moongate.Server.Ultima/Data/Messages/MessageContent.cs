namespace Moongate.Server.Ultima.Data.Messages;

/// <summary>
///     One text of <c>data/messages/&lt;language&gt;.toml</c> in the configured language, or in English when the language
///     does not have it.
/// </summary>
public class MessageContent
{
    /// <summary>
    ///     The number the code asks for, the same in every language.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The text as a .NET composite format: <c>{0}</c>, <c>{1}</c>, ... are the values the code fills in.
    /// </summary>
    public string Text { get; set; }
}
