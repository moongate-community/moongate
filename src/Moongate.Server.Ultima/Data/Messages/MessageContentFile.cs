namespace Moongate.Server.Ultima.Data.Messages;

/// <summary>
///     The root of <c>data/messages/&lt;language&gt;.toml</c>: a <c>[messages]</c> table of <c>number = "text"</c>. The
///     property name must match the table name, otherwise the file reads as empty without any error.
/// </summary>
internal sealed class MessageContentFile
{
    public Dictionary<string, string> Messages { get; set; } = [];
}
