using System.Net;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Server.Ultima.Services.Books;

/// <summary>
///     Presents saved plain text, checking packet capacity after HTML escaping.
/// </summary>
public static class BookGumpRenderer
{
    private const int ParchmentBackground = 9380;

    public static bool TryBuild(string title, string author, string content, out GumpInstance? gump)
    {
        gump = null;
        if (!BookTextValidation.IsValidText(title, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(author, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(content, BookTextValidation.ContentLimit))
        {
            return false;
        }

        var layout = new GumpLayout()
            .Add(new GumpBackground { GumpId = ParchmentBackground, Width = 440, Height = 480 })
            .Add(new GumpHtml { X = 40, Y = 35, Width = 360, Height = 40, Text = Html(title) })
            .Add(new GumpHtml { X = 40, Y = 80, Width = 360, Height = 30, Text = Html(author) })
            .Add(new GumpHtml { X = 40, Y = 120, Width = 360, Height = 315, Text = Html(content), Scrollbar = true });

        try
        {
            var built = layout.Build();
            _ = new GumpPacket(1, 1, 0, 0, built);
            _ = new CompressedGumpPacket(1, 1, 0, 0, built);
        }
        catch (ArgumentException)
        {
            return false;
        }

        gump = new() { Id = "readable_document", X = 100, Y = 50, Layout = layout, OnResponse = (_, _) => { } };
        return true;
    }

    private static string Html(string text)
    {
        return WebUtility.HtmlEncode(text).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
    }
}
