namespace Moongate.Server.Ultima.Data.Templates.Books;

public class BookTemplateSource
{
    public List<BookAttachmentSource> Attachments { get; set; } = [];
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> Variables { get; set; } = [];
    public string ItemTemplate { get; set; } = "readable_scroll";

    /// <summary>
    ///     The graphic the created item takes, such as the cover of a book; unset, the item keeps its template's.
    /// </summary>
    public int? ItemId { get; set; }
    public Dictionary<string, BookTranslation> Translations { get; set; } = new(StringComparer.Ordinal);
}
