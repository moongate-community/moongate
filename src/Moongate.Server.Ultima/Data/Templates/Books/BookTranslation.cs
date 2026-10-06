namespace Moongate.Server.Ultima.Data.Templates.Books;

public sealed class BookTranslation
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Content { get; set; }

    /// <summary>
    ///     Read only to be refused: the graphic is the item's, whatever the language of its text.
    /// </summary>
    public int? ItemId { get; set; }

    /// <summary>
    ///     Read only to be refused, as <see cref="ItemId" />: a book is writable in every language or in none.
    /// </summary>
    public bool? Writable { get; set; }

    /// <summary>
    ///     Read only to be refused, as <see cref="ItemId" />.
    /// </summary>
    public int? Pages { get; set; }
}
