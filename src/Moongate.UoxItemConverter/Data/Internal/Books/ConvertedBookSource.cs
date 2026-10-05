using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.UoxItemConverter.Data.Internal.Books;

/// <summary>
///     The plain document fields written by the importer, with an editable multiline body.
/// </summary>
internal sealed class ConvertedBookSource
{
    public required string Title { get; init; }
    public required string Author { get; init; }

    [TomlStringStyle(TomlStringStyle.MultilineBasic)]
    public required string Content { get; init; }

    public string ItemTemplate { get; init; } = "readable_scroll";
}
