using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Services.Internal.Books;

/// <summary>
///     Stores one validated document snapshot without live-world side effects.
/// </summary>
internal static class BookDocumentText
{
    /// <summary>
    ///     The prop of a book the character that carries it writes in.
    /// </summary>
    public const string WritableProp = "book.writable";

    /// <summary>
    ///     The prop with the page count of a writable book.
    /// </summary>
    public const string PagesProp = "book.pages";

    public static void Apply(ItemEntity item, RenderedBook rendered)
    {
        item.SetProp("book.template", rendered.TemplateId);
        item.SetProp("book.title", rendered.Title);
        item.SetProp("book.author", rendered.Author);
        item.SetProp("book.content", rendered.Content);
        item.Name = rendered.Title;

        if (rendered.Writable)
        {
            item.SetProp(WritableProp, true);
            item.SetProp(PagesProp, (long)rendered.Pages);
        }
        else
        {
            item.RemoveProp(WritableProp);
            item.RemoveProp(PagesProp);
        }

        if (rendered.ItemId is { } graphic)
        {
            item.ItemId = graphic;
        }
    }
}
