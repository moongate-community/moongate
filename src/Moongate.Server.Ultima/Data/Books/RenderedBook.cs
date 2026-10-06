namespace Moongate.Server.Ultima.Data.Books;

public sealed record RenderedBook
{
    public string TemplateId { get; init; } = "";
    public string ItemTemplateId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string Content { get; init; } = "";

    /// <summary>
    ///     The graphic the item takes; null leaves the item's own.
    /// </summary>
    public int? ItemId { get; init; }

    /// <summary>
    ///     Whether the character that carries the book writes in it.
    /// </summary>
    public bool Writable { get; init; }

    /// <summary>
    ///     The pages of a writable book; 0 for one that is not.
    /// </summary>
    public int Pages { get; init; }
}
