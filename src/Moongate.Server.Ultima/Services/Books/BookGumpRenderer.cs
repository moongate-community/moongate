using Moongate.Server.Core.Data.Sessions;
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
        return TryBuild(title, author, content, null, (_, _) => { }, out gump);
    }

    public static bool TryBuild(string title, string author, string content, string? claimLabel,
        Action<GameSession, GumpResponse> onResponse, out GumpInstance? gump)
    {
        gump = null;
        if (!BookTextValidation.IsValidText(title, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(author, BookTextValidation.HeaderLimit) ||
            !BookTextValidation.IsValidText(content, BookTextValidation.ContentLimit) ||
            (claimLabel is not null && !BookTextValidation.IsValidText(claimLabel, BookTextValidation.HeaderLimit)))
        {
            return false;
        }

        var layout = new GumpLayout()
            .Add(new GumpBackground { GumpId = ParchmentBackground, Width = 440, Height = 480 })
            .Add(new GumpHtml { X = 40, Y = 35, Width = 360, Height = 40, Text = Html(title) })
            .Add(new GumpHtml { X = 40, Y = 80, Width = 360, Height = 30, Text = Html(author) })
            .Add(new GumpHtml { X = 40, Y = 120, Width = 360, Height = claimLabel is null ? 315 : 285, Text = Html(content), Scrollbar = true });

        if (claimLabel is not null)
        {
            layout.Add(new GumpButton { X = 40, Y = 415, Up = 4005, Down = 4007, ButtonId = 1 })
                .Add(new GumpHtml { X = 80, Y = 415, Width = 320, Height = 30, Text = Html(claimLabel) });
        }

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

        gump = new() { Id = "readable_document", X = 100, Y = 50, Layout = layout, OnResponse = onResponse };
        return true;
    }

    private static string Html(string text)
    {
        return WebUtility.HtmlEncode(text).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
    }
}
