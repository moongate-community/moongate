using Moongate.Server.Ultima.Data.Templates.Books;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.UoxItemConverter.Data.Internal.Books;

/// <summary>
///     The document fields as the importer writes them when a multiline body would not keep the text exactly, such
///     as one that opens with a line end: every text is an escaped string.
/// </summary>
internal sealed class ConvertedPlainBookSource
{
    public required string Title { get; init; }

    public required string Author { get; init; }

    public required string Content { get; init; }

    public string ItemTemplate { get; init; } = "readable_book";

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public int? ItemId { get; init; }

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, BookTranslation>? Translations { get; init; }
}
