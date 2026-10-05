using Moongate.Server.Ultima.Data.Templates.Books;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.UoxItemConverter.Data.Internal.Books;

/// <summary>
///     The plain document fields written by the importer, with an editable multiline body. The translations are
///     written as <typeparamref name="TTranslation" /> says: multiline bodies when they keep the text exactly
///     (<see cref="ConvertedBookTranslation" />), plain strings otherwise (<see cref="BookTranslation" />).
/// </summary>
internal sealed class ConvertedBookSource<TTranslation>
{
    public required string Title { get; init; }
    public required string Author { get; init; }

    [TomlStringStyle(TomlStringStyle.MultilineBasic)]
    public required string Content { get; init; }

    public string ItemTemplate { get; init; } = "readable_book";

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public int? ItemId { get; init; }

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TTranslation>? Translations { get; init; }
}
