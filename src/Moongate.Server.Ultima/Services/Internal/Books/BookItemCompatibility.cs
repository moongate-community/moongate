using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services.Books;

namespace Moongate.Server.Ultima.Services.Internal.Books;

/// <summary>
///     Checks the document capabilities of the physical item selected for a starting book binding.
/// </summary>
internal static class BookItemCompatibility
{
    public static bool IsCompatible(BookTemplateSource source, ItemTemplate item)
    {
        if (item.Stackable != false || !BookTextValidation.IsReadableScript(item.ScriptId))
        {
            return false;
        }

        // Scripts choose the UI; template IDs and cover graphics do not. Native books can be edited,
        // while only the parchment UI exposes the attachment claim button.
        var nativeBook = item.ScriptId == BookTextValidation.BookScript;
        return (!source.Writable || nativeBook) && (source.Attachments.Count == 0 || !nativeBook);
    }
}
