namespace Moongate.Server.Ultima.Data.Templates.Books;

public class BookTemplateSource
{
    /// <summary>
    ///     The pages of a writable book that does not say how many it has.
    /// </summary>
    public const int DefaultPages = 20;

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

    /// <summary>
    ///     Whether the character that carries the created book writes in it; only for a book item.
    /// </summary>
    public bool Writable { get; set; }

    /// <summary>
    ///     How many pages a writable book has; unset, <see cref="DefaultPages" />.
    /// </summary>
    public int? Pages { get; set; }
    public Dictionary<string, BookTranslation> Translations { get; set; } = new(StringComparer.Ordinal);
}
