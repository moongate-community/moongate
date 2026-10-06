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

    /// <summary>
    ///     Whether the item is a book its carrier writes in. A prop that is not what it should be, set by a script
    ///     or by the staff, makes it one that is not.
    /// </summary>
    public static bool IsWritable(ItemEntity item)
    {
        try
        {
            return item.TryGetProp<bool>(WritableProp, out var writable) && writable;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>
    ///     The page count a writable book was made with; 0 when the item does not say, or says it wrongly.
    /// </summary>
    public static int PagesOf(ItemEntity item)
    {
        try
        {
            return item.TryGetProp<long>(PagesProp, out var pages) ? (int)Math.Clamp(pages, 0, int.MaxValue) : 0;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return 0;
        }
    }

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
