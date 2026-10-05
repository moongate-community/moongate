using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.UoxItemConverter.Data.Internal.Books;

/// <summary>
///     A kept translation as the importer writes it when its body can stay an editable multiline text.
/// </summary>
internal sealed class ConvertedBookTranslation
{
    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    public string? Author { get; init; }

    [TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingNull)]
    [TomlStringStyle(TomlStringStyle.MultilineBasic)]
    public string? Content { get; init; }
}
